using Microsoft.AspNetCore.Mvc;

namespace OrderFlow.Order.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public class IdempotentAttribute : ServiceFilterAttribute
{
    public IdempotentAttribute() : base(typeof(IdempotencyFilter))
    {
    }
}

