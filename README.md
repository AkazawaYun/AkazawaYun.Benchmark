# Description

This is a tool to benchmark server performance.

You can input some number to config the concurrency request following its tip.

It is written via C#, you may need to install the .net9 cli runtime to run this console app:

* [.net9 cli runtime download](https://dotnet.microsoft.com/en-us/download/dotnet/9.0/runtime) (select the first button: Run console apps 'Download x64')

You can create a file req.txt in the folder with the .exe , it will read it to as the tcp request data. 

* It read file only once when be launched, not reRead when start new benchmark after show previous result. 
If you edit the req.txt, just to launch a new app ! And the old one will keep the old requset-data to continue testing.

* If the file req.txt is not exist, it will be created automatically with this content:

```
GET /plaintext HTTP/1.1
Host: 127.0.0.1:8080
Connection: keep-alive


```

# Screenshot

![screenshot](https://akazawayun.cn/benchmark.png)


# Warn

It should check the Content-Length form the http-headers in fact. 

But in order to improve the performance...

It send two scout-request before the real benchmark, check if the two response data is same, and remember one's length, 
then use that length to split the tcp-response-stream to each http-response in the real benchmark.

So all the http-response from your server must be SAME length for the SAME request !

Of course, different request do not care this warn.
