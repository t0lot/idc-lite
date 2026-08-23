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
> Полноценный релиз **v2**: сверхнизкое потребление памяти (~20 МБ в фоне), поддержка современных процессоров и кроссплатформенная работа на **Linux**.

</div>

---

### 📌 Статус проекта: Финальный релиз (Feature Complete)

> **Проект завершён, отлажен и не заброшен.**  
> Это компактный локальный софт с чётко очерченным назначением. Все поставленные цели достигнуты на 100%: приложение полностью автономно, вычищено от утечек дескрипторов, оптимизировано по потреблению ОЗУ до рекордных **~20 МБ**, аппаратно поддерживает актуальные поколения процессоров Intel и AMD, а также получило нативный Linux-демон. Раздувать функционал и усложнять проект новыми слоями смысла нет — софт делает ровно то, для чего создавался, максимально быстро и надежно.

---

## 🎯 Зачем этот проект?

Родное приложение на базе Chromium/Electron — это боль: оно потребляет много ресурсов, может внезапно зависнуть под нагрузкой в игре и разбрасывает файлы по всей системе.

**IDC-Lite v2** полностью решает эту проблему:
- **Windows:** Нативное приложение на WPF (.NET 8) с агрессивным триммингом памяти при сворачивании в трей.
- **Linux:** Кроссплатформенный слой телеметрии (`hwmon`, `sysfs`, `/proc`) и прямой доступ к USB HID через `/dev/hidraw`.
- Программа не создает лишней нагрузки на систему и не зависает при 100% загрузке CPU.

---

## 💡 Сравнение с оригинальным софтом

| Параметр | Оригинал ID-COOLING | IDC-Lite v2 | Разница |
| :--- | :---: | :---: | :---: |
| 🪟 **ОЗУ (развернутое окно)** | ~200 МБ | **~100 МБ** | **В 2 раза легче** |
| 💤 **ОЗУ (в фоне / трее)** | ~50 МБ | **~20 МБ** | **В 2.5 раза легче** |
| 🚀 **Размер и вес приложения** | 150+ МБ (Electron) | **Один файл / ~25 МБ** | **В 6 раз компактнее** |
| 🐧 **Поддержка Linux** | ❌ Нет | **✅ Полная поддержка** | Эксклюзивно в v2 |
| 🌍 **Языки интерфейса** | EN / ZH | **RU / EN / ZH** | Полная локализация |
| 🎬 **Анимации дисплея** | Стандарт | **None / Smooth / Roller** | Настраиваемая динамика |

---

## ✨ Основные возможности

- ⚡ **Экстремально низкое потребление в фоне:** всего **~20 МБ ОЗУ** при сворачивании в системный трей благодаря автоматической очистке рабочего набора.
- 🌡️ **Мониторинг CPU в реальном времени:** вывод актуальной температуры, загрузки и частоты на LCD-дисплей СЖО.
- 🎯 **Гибкие источники данных:** автовыбор, средняя по ядрам, Hotspot или CPU Package (обновленный движок аппаратного мониторинга).
- 🐧 **Linux-совместимость:** сервисы опроса сенсоров ядра (`hwmon`, `coretemp`, `k10temp`, `zenpower`) и прямая отправка пакетов в `/dev/hidraw`.
- 📌 **Иконка в трее:** быстрый доступ к настройкам и информер состояния.
- 🚀 **Автозапуск:** старт вместе с системой и мгновенный запуск в свернутом виде.
- 🎨 **Приятный UI:** минималистичный интерфейс в тёмной палитре *Catppuccin Mocha*.

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

В версии **v2** реализован автономный легковесный фоновый сервис (демон):
- **Телеметрия:** Мониторинг процессора через стандартные интерфейсы подсистем ядра Linux (`/sys/class/hwmon/`, `/sys/class/thermal/`, `/proc/stat`, `/proc/cpuinfo`).
- **HID-контроллер:** Прямая передача 64-байтных управляющих кадров на устройство через `/dev/hidraw*`.

### Настройка прав доступа (udev rule)

Для работы с USB HID дисплеем без прав `root` добавьте правило udev:

```bash
echo 'SUBSYSTEM=="hidraw", ATTRS{idVendor}=="1a86", ATTRS{idProduct}=="e317", MODE="0666"' | sudo tee /etc/udev/rules.d/99-idcooling-hid.rules
sudo udevadm control --reload-rules && sudo udevadm trigger
```

### Запуск Linux демона
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
2. Измените константы и структуру фрейма в [`Services/HidService.cs`](https://github.com/t0lot/idc-lite/blob/main/idc-lite/Services/HidService.cs) и [`Services/LinuxHidService.cs`](https://github.com/t0lot/idc-lite/blob/main/idc-lite/Services/LinuxHidService.cs).

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
