# Build 

## make console project

```sh
dotnet new console -n <console project name>
```

## make class library

```sh
dotnet new classlib -n <class library project name>
```

## add class library to conosle project 

```sh
dotnet add <console project - *.csproj> reference <class library project - *.csproj>
```

## dotnet build

```sh
dotnet build 
or
dotnet run #build and run 
```