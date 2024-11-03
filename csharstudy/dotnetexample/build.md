# Build 

## make solution in this folder 

```sh
dotnet new sln -n <solution name>
```

## add project to solution

```sh
dotnet sln <Current Solution - *.sln> add <project - *.proj>
```

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