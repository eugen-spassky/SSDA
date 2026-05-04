# SSDA

Современный Steam Desktop Authenticator на WPF / .NET 8 — чёрно-белый минимализм поверх настоящего Mica/Acrylic.

## Что это

Полноценная замена [`jessecar96/SteamDesktopAuthenticator`](https://github.com/jessecar96/SteamDesktopAuthenticator) с другим дизайном.
Совместима с существующими `manifest.json` и `*.maFile` — переезд без перепривязки авторизатора.

## Статус

Этот PR — этап&#160;1 из 7 запланированных.

| Этап | Содержание | Статус |
|------|------------|--------|
| 1    | Каркас, Core, тесты, WPF shell с Mica | этот PR |
| 2    | Steam Web client + страница «Все подтверждения» с accept/deny | следующий |
| 3    | Логин в Steam через SteamKit2 (refresh / re-login) | планируется |
| 4    | Привязка нового авторизатора (5-шаговый степпер) | планируется |
| 5    | Импорт `.maFile` от оригинального SDA, миграция | планируется |
| 6    | Tray-иконка, hotkeys, command-line `-k`, `-s`, автостарт | планируется |
| 7    | Установщик / single-file build / signing pipeline | планируется |

## Стек

* WPF на .NET&#160;8 (`net8.0-windows`)
* Mica / Acrylic backdrop через `DwmSetWindowAttribute`
* `CommunityToolkit.Mvvm` для ViewModels
* Core (cross-platform `net8.0`) — TOTP, шифрование, manifest
* xUnit для тестов; `Rfc2898DeriveBytes.Pbkdf2` + `Aes` (CBC, PKCS7) — побайтово совместимы с оригинальным SDA

## Структура

```
src/
  SSDA.Core/                — модели, шифрование, TOTP, manifest, time aligner
  SSDA.App/                 — WPF UI, тема, окна
tests/
  SSDA.Core.Tests/          — xUnit
.github/workflows/ci.yml    — build + test + publish SSDA.exe
```

## Запуск из исходников

Требуется .NET&#160;8 SDK ([загрузка](https://dotnet.microsoft.com/download/dotnet/8.0)).

### Windows

```powershell
git clone https://github.com/eugen-spassky/SSDA.git
cd SSDA
dotnet build SSDA.sln -c Release
dotnet run --project src/SSDA.App/SSDA.App.csproj -c Release
```

Готовый `SSDA.exe` лежит после `dotnet publish`:

```powershell
dotnet publish src/SSDA.App/SSDA.App.csproj -c Release -r win-x64 --self-contained false -o publish
.\publish\SSDA.exe
```

### Linux / macOS

`SSDA.App` — WPF, его нельзя запустить вне Windows. На Linux собирается через `EnableWindowsTargeting=true`, но `.exe` запускать нечем (для разработки можно проверять Core + тесты).

```bash
dotnet test SSDA.sln                     # works on any OS
dotnet build src/SSDA.App/SSDA.App.csproj # builds Windows DLLs cross-platform
```

## Хранение maFiles

По умолчанию SSDA читает `%APPDATA%\SSDA\maFiles\`. Чтобы импортировать существующие файлы оригинального SDA — скопируйте `manifest.json` и `*.maFile` в эту папку.

## Безопасность

Это desktop-аутентификатор. Если ваша Windows-учётка скомпрометирована, заражённое ПО получает доступ и к паролю Steam, и к 2FA, и к подтверждениям. Steam рекомендует использовать только мобильное приложение. Проект публикуется как технический клон оригинала; используйте на свой страх и риск.

## Лицензия

Без лицензии — все права принадлежат автору репозитория.
