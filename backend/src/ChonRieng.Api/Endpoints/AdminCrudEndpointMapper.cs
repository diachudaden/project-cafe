using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ChonRieng.Application.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ChonRieng.Api.Endpoints;

/// <summary>A bounded page of administrator-managed resources.</summary>
public sealed record PageResponse<T>(int Page, int PageSize, IReadOnlyList<T> Items);

public static class AdminCrudEndpointMapper
{
    public static RouteGroupBuilder MapCrud<TEntity, TCreate, TUpdate, TResponse>(
        this RouteGroupBuilder group,
        string route,
        Func<TCreate, TEntity> create,
        Action<TEntity, TUpdate> update,
        Func<TEntity, TResponse> response,
        Func<TEntity, Guid> getId)
        where TEntity : class
        where TCreate : class
        where TUpdate : class
    {
        group.MapGet("/", async Task<Results<Ok<PageResponse<TResponse>>, BadRequest<ProblemDetails>>> (
            int? page,
            int? pageSize,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            if (page is < 1 || pageSize is < 1 or > 100)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid pagination",
                    Detail = "Page must be positive and pageSize must be between 1 and 100.",
                });
            }

            var currentPage = page ?? 1;
            var currentPageSize = pageSize ?? 50;
            if (currentPage > int.MaxValue / currentPageSize)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid pagination",
                    Detail = "The requested page is too large.",
                });
            }

            var entities = await service.ListAsync(currentPage, currentPageSize, cancellationToken);
            return TypedResults.Ok(new PageResponse<TResponse>(
                currentPage,
                currentPageSize,
                entities.Select(response).ToArray()));
        })
            .WithName($"List{route}")
            .WithSummary($"List {route.ToLowerInvariant()}")
            .WithDescription($"Returns a bounded page of {route.ToLowerInvariant()}.")
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async Task<Results<Ok<TResponse>, NotFound>> (
            Guid id,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            var entity = await service.FindAsync(id, cancellationToken);
            return entity is null ? TypedResults.NotFound() : TypedResults.Ok(response(entity));
        })
            .WithName($"Get{route}")
            .WithSummary($"Get {route.ToLowerInvariant()} by ID")
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            HttpContext httpContext,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            var request = await ReadRequestAsync<TCreate>(httpContext, cancellationToken);
            if (request is null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Request"] = ["The request body must contain a valid JSON object."],
                });
            }

            var errors = ValidateRequest(request);
            if (errors.Count != 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var entity = await service.CreateAsync(create(request), cancellationToken);
            var id = getId(entity);
            return (IResult)TypedResults.Created($"/api/admin/{route.ToLowerInvariant()}/{id}", response(entity));
        })
            .WithName($"Create{route}")
            .WithSummary($"Create {route.ToLowerInvariant()}")
            .Accepts<TCreate>("application/json")
            .Produces<TResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", async (
            HttpContext httpContext,
            Guid id,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            var request = await ReadRequestAsync<TUpdate>(httpContext, cancellationToken);
            if (request is null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Request"] = ["The request body must contain a valid JSON object."],
                });
            }

            var errors = ValidateRequest(request);
            if (errors.Count != 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var entity = await service.UpdateAsync(id, item => update(item, request), cancellationToken);
            return entity is null
                ? (IResult)TypedResults.NotFound()
                : TypedResults.Ok(response(entity));
        })
            .WithName($"Update{route}")
            .WithSummary($"Update {route.ToLowerInvariant()}")
            .Accepts<TUpdate>("application/json")
            .Produces<TResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            return await service.DeleteAsync(id, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound();
        })
            .WithName($"Delete{route}")
            .WithSummary($"Delete {route.ToLowerInvariant()}")
            .WithDescription("Deletion can return 409 when related records prevent removal.")
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<TRequest?> ReadRequestAsync<TRequest>(
        HttpContext httpContext,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        try
        {
            return await httpContext.Request.ReadFromJsonAsync<TRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, string[]> ValidateRequest<TRequest>(TRequest request)
        where TRequest : class
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
        return validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty("Request")
                .Select(member => new { Member = member, result.ErrorMessage }))
            .GroupBy(item => item.Member)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ErrorMessage ?? "The value is invalid.").ToArray());
    }
}
