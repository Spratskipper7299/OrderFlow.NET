using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Order.Api.Filters;
using OrderFlow.Order.Application.Orders.Commands.CreateOrder;
using OrderFlow.Order.Application.Orders.Queries.GetOrder;
using OrderFlow.Order.Domain;

namespace OrderFlow.Order.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(CreateOrderResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetOrder), new { id = result.OrderId }, result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetOrderResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOrderQuery(id), cancellationToken);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
