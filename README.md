## Community.AspNetCore.JsonRpc

Provides [JSON-RPC 2.0](http://www.jsonrpc.org/specification) support for ASP.NET Core based on [JSON-RPC 2.0 Transport: HTTP](https://www.simple-is-better.org/json-rpc/transport_http.html) proposal.

[![NuGet package](https://img.shields.io/nuget/v/Community.AspNetCore.JsonRpc.svg?style=flat-square)](https://www.nuget.org/packages/Community.AspNetCore.JsonRpc)

### Sample of using JSON-RPC middleware

1. Define types for request parameters and result objects:

```cs
class CalculatorOperands
{
    [JsonProperty("operand_1")]
    public double Operand1 { get; set; }

    [JsonProperty("operand_2")]
    public double Operand2 { get; set; }
}
```

2. Implement RPC-JSON handler or service interface:

```cs
class JsonRpcCalculatorHandler : IJsonRpcHandler
{
    public JsonRpcSerializerScheme CreateScheme()
    {
        var result = new JsonRpcSerializerScheme();

        result.Methods["plus"] = new JsonRpcMethodScheme(false, typeof(CalculatorOperands));

        return result;
    }

    public Task HandleNotification(JsonRpcRequest request)
    {
        throw new JsonRpcException("Notifications are not supported");
    }

    public Task<JsonRpcResponse> HandleRequest(JsonRpcRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var operands = (CalculatorOperands)request.Params;
        var response = default(JsonRpcResponse);

        switch (request.Method)
        {
            case "plus":
                {
                    response = new JsonRpcResponse(operands.Operand1 + operands.Operand2, request.Id);
                }
                break;
            default:
                {
                    throw new JsonRpcException($"Unsupported operation: \"{request.Method}\"");
                }
        }

        return Task.FromResult(response);
    }
}
```
or
```cs
class JsonRpcCalculatorService : IJsonRpcService
{
    [JsonRpcMethod("plus")]
    public Task<double> Plus(CalculatorOperands operands)
    {
        return Task.FromResult(operands.Operand1 + operands.Operand2);
    }
}
```

3. Register implemented handler or service in web host builder:

```cs
builder.Configure(app => app.UseJsonRpc("/calculator", new JsonRpcCalculatorHandler()))
```
or
```cs
builder.Configure(app => app.UseJsonRpc("/calculator", new JsonRpcCalculatorService()))
```