using APILab.Data;
using APILab.Endpoints;
using APILab.Serialization;
using APILab.Services.Soap;
using APILab.Components;
using Scalar.AspNetCore;
using SoapCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSoapCore();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<FeatureFlagStore>();
builder.Services.AddSingleton(provider => new TodoStore(
    Path.Combine(builder.Environment.ContentRootPath, "Data", "todos.db"),
    [
        new(1, "Walk the dog"),
        new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
        new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
        new(4, "Clean the bathroom"),
        new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
    ],
    provider.GetRequiredService<FeatureFlagStore>()));
builder.Services.AddSingleton<TodoSoapService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseStaticFiles();
app.UseAntiforgery();

var todosApi = app.MapGroup("/todos");
todosApi.MapGetTodos();
todosApi.MapGetTodoById();
todosApi.MapCreateTodo();
todosApi.MapUpdateTodo();
todosApi.MapDeleteTodo();

((IApplicationBuilder)app).UseSoapEndpoint<TodoSoapService>("/TodoService.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);

    app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
