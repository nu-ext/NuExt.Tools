# NuExt.Tools.ProjectMerger

Merges project files into one or more text files suitable for AI analysis.

## Installation

```shell
dotnet tool install NuExt.Tools.ProjectMerger
```

## Usage

Merge files from the current directory:

```shell
nuext-merge
```

Merge files from the specified directory:

```shell
nuext-merge <path>
```

Merge all files into a single output file:

```shell
nuext-merge <path> --single
```

Use a custom list of file extensions:

```shell
nuext-merge <path> ".cs;.xaml;.csproj"
```

## Options

```text
--single    Merge all files into a single output file.
--help      Show command-line help.
```

## License

MIT. See LICENSE.