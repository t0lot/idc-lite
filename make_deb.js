const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

function createUstarHeader(filename, size, mode, typeflag) {
    const buf = Buffer.alloc(512);
    buf.write(filename, 0, 100, 'utf8');
    buf.write(mode.toString(8).padStart(7, '0') + '\0', 100, 8, 'ascii');
    buf.write('0000000\0', 108, 8, 'ascii'); // uid
    buf.write('0000000\0', 116, 8, 'ascii'); // gid
    buf.write(size.toString(8).padStart(11, '0') + '\0', 124, 12, 'ascii');
    const mtime = Math.floor(Date.now() / 1000).toString(8).padStart(11, '0') + '\0';
    buf.write(mtime, 136, 12, 'ascii');
    buf.fill(0x20, 148, 156); // checksum spaces
    buf.write(typeflag, 156, 1, 'ascii');
    buf.write('ustar\0', 257, 6, 'ascii');
    buf.write('00', 263, 2, 'ascii');

    let sum = 0;
    for (let i = 0; i < 512; i++) sum += buf[i];
    buf.write(sum.toString(8).padStart(6, '0') + '\0 ', 148, 8, 'ascii');
    return buf;
}

function tarDir(baseDir, prefix = './') {
    const chunks = [];
    function walk(currDir, currTarPath) {
        const entries = fs.readdirSync(currDir, { withFileTypes: true });
        for (const entry of entries) {
            const fullPath = path.join(currDir, entry.name);
            const tarPath = currTarPath + entry.name + (entry.isDirectory() ? '/' : '');
            if (entry.isDirectory()) {
                chunks.push(createUstarHeader(tarPath, 0, 0o755, '5'));
                walk(fullPath, tarPath);
            } else {
                const stat = fs.statSync(fullPath);
                const isExec = entry.name.indexOf('.') === -1 || entry.name.endsWith('.sh') || entry.name === 'postinst' || entry.name === 'prerm';
                const mode = isExec ? 0o755 : 0o644;
                chunks.push(createUstarHeader(tarPath, stat.size, mode, '0'));
                const fileData = fs.readFileSync(fullPath);
                chunks.push(fileData);
                const pad = 512 - (stat.size % 512);
                if (pad < 512) chunks.push(Buffer.alloc(pad));
            }
        }
    }
    walk(baseDir, prefix);
    chunks.push(Buffer.alloc(1024));
    return Buffer.concat(chunks);
}

function createArHeader(name, size) {
    const buf = Buffer.alloc(60, 0x20);
    buf.write(name.padEnd(16, ' '), 0, 16, 'ascii');
    buf.write(Math.floor(Date.now() / 1000).toString().padEnd(12, ' '), 16, 12, 'ascii');
    buf.write('0'.padEnd(6, ' '), 28, 6, 'ascii');
    buf.write('0'.padEnd(6, ' '), 34, 6, 'ascii');
    buf.write('100644'.padEnd(8, ' '), 40, 8, 'ascii');
    buf.write(size.toString().padEnd(10, ' '), 48, 10, 'ascii');
    buf.write('\x60\x0a', 58, 2, 'binary');
    return buf;
}

function packageDeb(debDir, outPath) {
    const debianBinary = Buffer.from('2.0\n', 'ascii');
    const controlTarGz = zlib.gzipSync(tarDir(path.join(debDir, 'DEBIAN'), './'));
    
    // data tar (exclude DEBIAN)
    const dataChunks = [];
    function walkData(currDir, currTarPath) {
        const entries = fs.readdirSync(currDir, { withFileTypes: true });
        for (const entry of entries) {
            if (entry.name === 'DEBIAN') continue;
            const fullPath = path.join(currDir, entry.name);
            const tarPath = currTarPath + entry.name + (entry.isDirectory() ? '/' : '');
            if (entry.isDirectory()) {
                dataChunks.push(createUstarHeader(tarPath, 0, 0o755, '5'));
                walkData(fullPath, tarPath);
            } else {
                const stat = fs.statSync(fullPath);
                const isExec = entry.name.indexOf('.') === -1;
                const mode = isExec ? 0o755 : 0o644;
                dataChunks.push(createUstarHeader(tarPath, stat.size, mode, '0'));
                const fileData = fs.readFileSync(fullPath);
                dataChunks.push(fileData);
                const pad = 512 - (stat.size % 512);
                if (pad < 512) dataChunks.push(Buffer.alloc(pad));
            }
        }
    }
    walkData(debDir, './');
    dataChunks.push(Buffer.alloc(1024));
    const dataTarGz = zlib.gzipSync(Buffer.concat(dataChunks));

    const arMagic = Buffer.from('!<arch>\n', 'ascii');
    const parts = [
        arMagic,
        createArHeader('debian-binary', debianBinary.length),
        debianBinary,
        createArHeader('control.tar.gz', controlTarGz.length),
        controlTarGz,
        controlTarGz.length % 2 !== 0 ? Buffer.from('\n') : Buffer.alloc(0),
        createArHeader('data.tar.gz', dataTarGz.length),
        dataTarGz,
        dataTarGz.length % 2 !== 0 ? Buffer.from('\n') : Buffer.alloc(0)
    ];

    fs.writeFileSync(outPath, Buffer.concat(parts));
    console.log(`Created ${outPath} (${fs.statSync(outPath).size} bytes)`);
}

if (!fs.existsSync('dist')) fs.mkdirSync('dist');
packageDeb('deb_build/x64', 'dist/idc-daemon-v2-amd64.deb');
packageDeb('deb_build/arm64', 'dist/idc-daemon-v2-arm64.deb');
