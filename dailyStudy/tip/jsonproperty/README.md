# json converter 

## json write 시 문제 점

- string object로 되어 있는 json object 객체를 jobject 로 read한 뒤 다시 write할 경우 `"\` 문자열이 포함 되어 json 파일 open 시 제대로 정렬이 안되는 문제 발생 

- [FlexibleStringConverter.cs](./FlexibleStringConverter.cs) 
