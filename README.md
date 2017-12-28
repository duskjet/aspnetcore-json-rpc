## Community.AspNetCore.JsonRpc

Provides [JSON-RPC 2.0](http://www.jsonrpc.org/specification) support for ASP.NET Core based on [JSON-RPC 2.0 Transport: HTTP](https://www.simple-is-better.org/json-rpc/transport_http.html) proposal.

[![NuGet package](https://img.shields.io/nuget/v/Community.AspNetCore.JsonRpc.svg?style=flat-square)](https://www.nuget.org/packages/Community.AspNetCore.JsonRpc)

### Sample of using JSON-RPC middleware

1. Implement a handler or service:

```cs
class CalculatorHandler : IJsonRpcHandler
{
    public JsonRpcSerializerScheme CreateScheme()
    {
        var scheme = new JsonRpcSerializerScheme();

        scheme.Methods["pin"] = new JsonRpcMethodScheme();
        scheme.Methods["acl"] = new JsonRpcMethodScheme();
        scheme.Methods["add"] = new JsonRpcMethodScheme(
            new[]
            {
                typeof(long),
                typeof(long)
            });
        scheme.Methods["sub"] = new JsonRpcMethodScheme(
            new Dictionary<string, Type>
            {
                ["p1"] = typeof(long),
                ["p2"] = typeof(long)
            });

        return scheme;
    }

    public Task<JsonRpcResponse> Handle(JsonRpcRequest request)
    {
        var response = default(JsonRpcResponse);

        switch (request.Method)
        {
            case "pin":
                {
                }
                break;
            case "acl":
                {
                    var error = new JsonRpcError(100L, "Operation is not available");
                    
                    response = new JsonRpcResponse(error, request.Id);
                }
                break;
            case "add":
                {
                    var operand1 = request.ParamsByPosition[0];
                    var operand2 = request.ParamsByPosition[1];
                    var result = operand1 - operand2;
                    
                    response = new JsonRpcResponse(result, request.Id);
                }
                break;
            case "sub":
                {
                    var operand1 = request.ParamsByName["o1"];
                    var operand2 = request.ParamsByName["o2"];
                    var result = operand1 - operand2;
                    
                    response = new JsonRpcResponse(result, request.Id);
                }
                break;
        }

        return Task.FromResult(response);
    }
}
```
```cs
class CalculatorService
{
    [JsonRpcName("pin")]
    public Task Ping()
    {
        return Task.CompletedTask;
    }

    [JsonRpcName("acl")]
    public Task Clear()
    {
        throw new JsonRpcServiceException(100L, "Operation is not available");
    }

    [JsonRpcName("add")]
    public Task<long> Add(long operand1, long operand2)
    {
        return Task.FromResult(operand1 + operand2);
    }

    [JsonRpcName("sub")]
    public Task<long> Substract([JsonRpcName("o1")]long operand1, [JsonRpcName("o2")]long operand2)
    {
        return Task.FromResult(operand1 - operand2);
    }
}
```

2. Register the implemented handler or service in web host builder:

```cs
builder.Configure(app => app.UseJsonRpcHandler("/calculator", new JsonRpcCalculatorHandler()))
```
```cs
builder.Configure(app => app.UseJsonRpcService("/calculator", new JsonRpcCalculatorService()))
```