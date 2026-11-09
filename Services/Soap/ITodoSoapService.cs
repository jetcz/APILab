using APILab.Models;
using System.ServiceModel;

namespace APILab.Services.Soap;

[ServiceContract]
public interface ITodoSoapService
{
    [OperationContract]
    SoapTodo[] GetTodos();

    [OperationContract]
    SoapTodo? GetTodoById(int id);

    [OperationContract]
    SoapTodo CreateTodo(SoapTodo todo);

    [OperationContract]
    SoapTodo? UpdateTodo(int id, SoapTodo todo);

    [OperationContract]
    bool DeleteTodo(int id);
}