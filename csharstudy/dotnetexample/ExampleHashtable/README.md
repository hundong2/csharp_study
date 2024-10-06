# ExampleHash

## create library project

```sh
dotnet new classlib -n ProjectName
```

## create console project

```sh
dotnet new console -n ProjectName
```

## add library project to console project

```sh
dotnet add <YourExeProjectName> reference <YourLibraryProjectName>
```

## delete library project from console project 

```sh
dotnet remove <console project Name> reference <deleted library project Name>
```

## build & run

```sh
dotnet build 

or 

dotnet run 
```
