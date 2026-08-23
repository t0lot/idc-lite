<div align="center">

# ❄️ IDC-Lite v2 (Final)

**Нативная сверхлегкая утилита для управления LCD-дисплеем СЖО ID-COOLING серии FX (Windows & Linux)**

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-blue)](README.md)

<br />

<img src="images/1.png" alt="Главное окно IDC-Lite" width="300">

<br />
<br />

> ⚡ **Чистый C# / .NET без лишнего мусора.**  
> Полноценный релиз **v2**: сверхнизкое потребление памяти (~20 МБ в фоне), поддержка современных процессоров и **первая в мире нативная поддержка Linux** (официальный софт от ID-COOLING под Linux отсутствует в принципе).

</div>

---

### 📌 Статус проекта: Финальный релиз (Feature-Complete)

> **Проект завершён, отлажен и не заброшен.**  
> Это компактный локальный софт с чётко очерченным назначением. Все поставленные цели достигнуты на 100%: приложение полностью автономно, вычищено от утечек дескрипторов, оптимизировано по потреблению ОЗУ до рекордных **~20 МБ**, аппаратно поддерживает актуальные поколения процессоров Intel и AMD, а также получило нативный Linux-демон. Раздувать функционал и усложнять проект новыми слоями смысла нет — софт делает ровно то, для чего создавался, максимально быстро и надежно.

---

## 🎯 Зачем этот проект?

1. **Официальный софт ID-COOLING под Linux НЕ СУЩЕСТВУЕТ ВООБЩЕ**: пользователи Linux с СЖО ID-COOLING FX LCD оставались с неработающим экраном. **IDC-Lite** дает первое и полноценное нативное решение (daemon + systemd + udev) без сторонних драйверов.
2. **Официальный софт для Windows на базе Chromium/Electron — тяжелый и нестабильный**: потребляет до 200+ МБ ОЗУ, зависает под нагрузкой в играх и разбрасывает файлы по системе.

**IDC-Lite v2** полностью закрывает эти проблемы:
- **Windows:** Нативное приложение на WPF (.NET 8) со сбросом рабочего набора памяти (`Working Set Trimming`) при сворачивании в трей (**~20 МБ**).
- **Linux:** Кроссплатформенный демон (`idc-daemon`), напрямую работающий через подсистемы ядра `hwmon`/`sysfs`/`/proc` и посылающий HID-фреймы в `/dev/hidraw`.
- Программа не создает паразитной нагрузки на систему и не зависает при 100% загрузке CPU.

---

## 💡 Сравнение с оригинальным софтом

| Параметр | Оригинал ID-COOLING | IDC-Lite v2 | Преимущество |
| :--- | :---: | :---: | :---: |
| 🐧 **Поддержка Linux** | ❌ **Отсутствует в принципе** | **✅ Нативный демон (.deb / tar.gz)** | **Эксклюзив IDC-Lite** |
| 💤 **ОЗУ (в фоне / трее)** | ~50 МБ | **~20 МБ** | **В 2.5 раза легче** |
| 🪟 **ОЗУ (развернутое окно)** | ~200 МБ | **~100 МБ** | **В 2 раза легче** |
| 🚀 **Размер и вес приложения** | 150+ МБ (Electron) | **Один файл / ~25 МБ** | **В 6 раз компактнее** |
| 🌍 **Языки интерфейса** | EN / ZH | **RU / EN / ZH** | Полная локализация |
| 🎬 **Анимации дисплея** | Стандарт | **None / Smooth / Roller** | Настраиваемая динамика |

---

## ✨ Основные возможности

- 🐧 **Linux из коробки:** Полная поддержка Linux через `idc-daemon` (x86_64 и ARM64). Официального аналога на Linux нет!
- ⚡ **Экстремально низкое потребление в фоне:** всего **~20 МБ ОЗУ** при сворачивании в системный трей благодаря автоматической очистке рабочего набора.
- 🌡️ **Мониторинг CPU в реальном времени:** вывод актуальной температуры, загрузки и частоты на LCD-дисплей СЖО.
- 🎯 **Гибкие источники данных:** автовыбор, средняя по ядрам, Hotspot или CPU Package (обновленный движок аппаратного мониторинга).
- 📌 **Иконка в трее:** быстрый доступ к настройкам и информер состояния (исправлены утечки GDI дескрипторов).
- 🚀 **Автозапуск:** старт вместе с системой и мгновенный запуск в свернутом виде.
- 🎨 **Приятный UI:** минималистичный интерфейс в тёмной палитре *Catppuccin Mocha*.

---

## 🏗️ Структура проекта и зависимости

### Структура репозитория
```text
idc-lite/
├── deb_build/                   # Шаблоны сборки .deb пакетов (control, postinst, prerm, systemd, udev)
│   ├── amd64/
│   └── arm64/
├── idc-daemon/                  # Нативный фоновый демон для Linux
│   ├── Program.cs               # Точка входа, опрос hwmon/sysfs и отправка кадров в /dev/hidraw
│   └── idc-daemon.csproj        # Конфигурация проекта (.NET 8 console, single-file self-contained)
├── idc-lite/                    # Основное десктопное приложение для Windows
│   ├── Models/                  # Модели данных (AppSettings, Language)
│   ├── Resources/               # Иконки и ресурсы (.ico, .png)
│   ├── Services/                # Бизнес-логика, телеметрия и аппаратные драйверы
│   │   ├── AutostartService.cs         # Управление автозагрузкой
│   │   ├── DriverService.cs            # Обслуживание системного драйвера
│   │   ├── HardwareService.cs          # Чтение сенсоров Windows (LHM + WMI)
│   │   ├── HidService.cs               # Win32 HID-протокол (CreateFileW / WriteFile)
│   │   ├── LinuxHardwareService.cs     # Кроссплатформенное чтение sysfs/hwmon
│   │   ├── LinuxHidService.cs          # POSIX HID-протокол (/dev/hidraw)
│   │   ├── SettingsService.cs          # Сохранение конфигурации в AppData
│   │   ├── TaskSchedulerService.cs     # Автозапуск через Планировщик Windows
│   │   └── TranslationService.cs       # Словарь локализации (RU / EN / ZH)
│   ├── App.xaml / App.xaml.cs          # Трей, жизненный цикл и хуки памяти
│   ├── MainWindow.xaml / .cs           # Интерфейс WPF (Catppuccin Mocha)
│   └── idc-lite.csproj                 # Конфигурация сборки Windows x64
├── images/                      # Скриншоты интерфейса
├── .gitignore                   # Правила исключения временных файлов и бинарников
├── LICENSE                      # Лицензия MIT
└── README.md                    # Документация проекта
```

### Зависимости проекта
- **`LibreHardwareMonitorLib` (v0.9.6)** — Чтение низкоуровневых сенсоров процессоров, включая новейшие микроархитектуры Intel Core Ultra (Arrow Lake/Meteor Lake) и AMD Ryzen 9000 (Zen 5).
- **`System.Management` (v10.0.2)** — Прямое обращение к WMI для резервного считывания температурных зон материнской платы (`MSAcpi_ThermalZoneTemperature`).

---

## 🖼️ Скриншоты

<div align="center">

  <img src="images/2.png" alt="Окно настроек IDC-Lite" width="320">
  <img src="images/3.png" alt="Окно настроек IDC-Lite" width="320">

  <br />

  <img src="images/4.png" alt="Окно настроек IDC-Lite" width="320">
  <img src="images/5.png" alt="Окно настроек IDC-Lite" width="320">

  <p><i>Интерфейс и настройки программы</i></p>

</div>

---

## 🐧 Поддержка Linux (`idc-daemon`)

У ID-COOLING **нет официального софта под Linux**. IDC-Lite полностью решает эту задачу с помощью фонового демона `idc-daemon`:
- **Телеметрия:** Мониторинг процессора через стандартные интерфейсы подсистем ядра Linux (`/sys/class/hwmon/`, `/sys/class/thermal/`, `/proc/stat`, `/proc/cpuinfo`). Поддерживает модули `coretemp`, `k10temp`, `zenpower` и `acpitz`.
- **HID-контроллер:** Прямая передача 64-байтных управляющих кадров на устройство через `/dev/hidraw*`.

### Быстрая установка (.deb пакет)
Для Ubuntu / Debian / Linux Mint / Pop!_OS:
```bash
# x86_64:
sudo dpkg -i idc-daemon-v2-amd64.deb

# ARM64:
sudo dpkg -i idc-daemon-v2-arm64.deb
```
*(Пакет автоматически установит бинарник в `/usr/local/bin`, применит правила `udev` и запустит `systemd`-сервис)*.

### Ручная настройка прав доступа (udev rule)
Если вы запускаете бинарник вручную из `.tar.gz`:
```bash
echo 'SUBSYSTEM=="hidraw", ATTRS{idVendor}=="1a86", ATTRS{idProduct}=="e317", MODE="0666", TAG+="uaccess"' | sudo tee /etc/udev/rules.d/99-idcooling-hid.rules
sudo udevadm control --reload-rules && sudo udevadm trigger
```

### Ручной запуск Linux демона
```bash
chmod +x idc-daemon
./idc-daemon
```

---

## 🛠️ Разработчикам & Архитектура

<details>
<summary><b>🔍 Системные вызовы и протокол обмена</b></summary>

<br />

| Платформа | Механизм | Назначение |
| :--- | :--- | :--- |
| **Windows** | `CreateFileW` / `WriteFile` (`kernel32.dll`) | Прямая отправка HID-отчётов на дисплей |
| **Windows** | `SetProcessWorkingSetSize` (`kernel32.dll`) | Сброс рабочего набора памяти до ~20 МБ в фоне |
| **Windows** | `DestroyIcon` (`user32.dll`) | Безопасная очистка дескрипторов GDI иконки трея |
| **Linux** | `open` / `write` / `close` (`libc.so`) | Прямая отправка кадров в `/dev/hidraw*` |
| **Linux** | `sysfs / procfs` | Чтение термодатчиков (`hwmon`), загрузки и тактовой частоты |

</details>

<details>
<summary><b>🛠️ Поддержка других СЖО (Реверс-инжиниринг)</b></summary>

<br />

Утилита оптимизирована под VID/PID `1A86:E317` и байты протокола `0x55`, `0xBB`.  
Если у вас другая модель с HID-дисплеем:
1. Снимите дампы USB-трафика через Wireshark / USBPcap.
2. Измените константы и структуру фрейма в [`Services/HidService.cs`](https://github.com/t0lot/idc-lite/blob/main/idc-lite/Services/HidService.cs) и [`idc-daemon/Program.cs`](https://github.com/t0lot/idc-lite/blob/main/idc-daemon/Program.cs).

</details>

---

## 📦 Сборка из исходников

### Windows (GUI)
```bash
# Клонировать репозиторий
git clone https://github.com/t0lot/idc-lite.git
cd idc-lite/idc-lite

# Скомпилировать автономный .exe
dotnet publish -c Release -r win-x64 --self-contained
```

### Linux (Daemon)
```bash
cd idc-lite/idc-daemon

# Сборка под Linux x64 / ARM64
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r linux-arm64 --self-contained
```
