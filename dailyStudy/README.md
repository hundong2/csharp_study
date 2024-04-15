# dotnet daily study pool 

## dotnet project setting information

1. dontet add class library  

example ( project1 <- stringLib )  

```sh
dotnet new classLib -n stringLib
dotnet add project1/project1.csproj reference stringLib/stringLib.csproj 
```  
2. dotnet project add to solution 

exampe ( solution = SolutionDailyApril, project = stringLib.csproj )

```sh
dotnet sln SolutionDailyApril.sln add stringLib/stringLib.csproj  
```  

solution project list check  

```sh
dotnet sln <solution *sln> list  
```

3. dotnet make solution  

example 

```sh
dotnet new sln  
```

4. dotnet Add Console Project  

example 

```sh
dotnet new console -n <project name>  
```



## 24.04.13

- https://learn.microsoft.com/ko-kr/dotnet/core/tools/dotnet-new-install
- https://medium.com/@kmorpex/10-advanced-c-tricks-for-experienced-developers-26a48c6a8c9c
- [learn example](https://learn.microsoft.com/ko-kr/dotnet/core/tutorials/library-with-visual-studio-code?pivots=dotnet-8-0)  
- 
