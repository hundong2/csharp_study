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

## Reflection

- [reflection path](./chapter6Example/Reflection/reflection.md)

## .NET Project 구조

- [structure of sln](./projectStructure.md). 

## Sealed 란?

- [What's the sealed](./sealed.md). 

## BigInteger

- [BigInteger](./chapter6Example/BigIntegerEx.cs)  
- [BigInteger](./chapter6Example/BigInteger.md). 

## IntPtr

- [IntPtr](./chapter6Example/IntPtr.md). 
- [IntPtr Example](./chapter6Example/IntPtrEx.cs). 



