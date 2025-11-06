This is a tool to benchmark server performance.

You can input some number to config the concurrency request following its tip.

It is written via C#, you may need to install the .net9 cli runtime to run this console app:

[.net9 cli runtime download](https://dotnet.microsoft.com/en-us/download/dotnet/9.0/runtime) (select the first button: Run console apps 'Download x64')

You can create a file req.txt in the folder with the .exe , it will read it to as the tcp request data.

If the file req.txt is not exist, it will be created automatically with this content:

```
GET /plaintext HTTP/1.1
Host: 127.0.0.1:8080
Connection: keep-alive


```
