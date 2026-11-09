# Installation

Spriggit is a command-line application for macOS, Linux, and Windows. Building and installing translation tools require the .NET 10 SDK.

```sh
dotnet build Spriggit.slnx -c Release
dotnet run --project Spriggit.CLI -c Release -- --help
```

Install the locally built CLI as a .NET tool:

```sh
dotnet pack Spriggit.CLI -c Release -o ./artifacts
dotnet tool install --global Spriggit.CLI --add-source ./artifacts --prerelease
spriggit --help
```

Convert a Skyrim SE plugin to YAML:

```sh
spriggit serialize -i "/path/to/Data/SomeMod.esp" -o "/path/to/repository/SomeMod" -g SkyrimSE -p Spriggit.Yaml
```

Convert the text back to a plugin (the output file is overwritten):

```sh
spriggit deserialize -i "/path/to/repository/SomeMod" -o "/path/to/Data/SomeMod.esp"
```

Use `spriggit <command> --help` for conversion, upgrade, standardize, and merge options. Translation packages are downloaded from NuGet on first use and require network access.

To publish a standalone executable for your machine:

```sh
dotnet publish Spriggit.CLI -c Release -r osx-arm64 --self-contained true -p:PackAsTool=false -p:PublishSingleFile=true -o ./publish
./publish/Spriggit.CLI --help
```

Replace `osx-arm64` with `osx-x64`, `linux-x64`, `linux-arm64`, `win-x64`, or `win-arm64` as needed. Windows executables have an `.exe` suffix.


See [CLI commands](cli.md) for all options.

Historical translation packages through 0.35.1 require Windows-only CLI releases. Upgrade projects using those packages on Windows before converting them on macOS or Linux.
