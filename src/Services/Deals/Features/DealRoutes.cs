using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseCRM.Deals.Domain;
using PulseCRM.Deals.Features.Deals;
using PulseCRM.Deals.Features.Pipeline;
using PulseCRM.Deals.Infrastructure;

namespace PulseCRM.Deals.Features;

public static class DealRoutes
{
    public static void MapDealRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/deals/pipelines/default", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDefaultPipelineQuery());
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/deals/{id:guid}", async (Guid id, DealsDbContext db) =>
        {
            var deal = await db.Deals
                .Include(d => d.Owner)
                .Include(d => d.Contact)
                .Include(d => d.Stage)
                .Include(d => d.Activities.OrderByDescending(a => a.CreatedAt).Take(50))
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deal is null) return Results.NotFound();

            var dto = deal.ToDto();
            return Results.Ok(new
            {
                Deal = dto,
                Activities = deal.Activities.Select(a => a.ToDto(a.Actor?.DisplayName ?? "Unknown")).ToList()
            });
        });

        group.MapPost("/deals", async ([FromBody] CreateDealCommand cmd, IMediator mediator) =>
        {
            if (string.IsNullOrWhiteSpace(cmd.Title) || cmd.Title.Length > 500)
                return Results.BadRequest("Title is required and must be under 500 characters");
            try
            {
                var result = await mediator.Send(cmd);
                return Results.Created($"/api/deals/{result.Id}", result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(ex.Message); }
        });

        group.MapMethods("/deals/{id:guid}", ["PATCH"], async (Guid id, [FromBody] UpdateDealRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var userId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
            var cmd = new UpdateDealCommand(id, req.Title, req.Value, req.OwnerId, req.ContactId, req.ExpectedCloseDate, userId);
            try { return Results.Ok(await mediator.Send(cmd)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        group.MapPost("/deals/{id:guid}/move", async (Guid id, [FromBody] MoveDealRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var userId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
            var cmd = new MoveDealCommand(id, req.StageId, req.PositionInStage, req.RowVersion, userId);
            try
            {
                var result = await mediator.Send(cmd);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                // Reload fresh deal and return as conflict payload — don't expose raw DB values
                var fresh = await mediator.Send(new GetDealByIdQuery(id));
                return Results.Conflict(fresh);
            }
        });

        group.MapPost("/deals/{id:guid}/notes", async (Guid id, [FromBody] AddNoteRequest req, IMediator mediator, ITenantContext tenant, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Content))
                return Results.BadRequest("Note content is required");
            if (req.Content.Length > 5000)
                return Results.BadRequest("Note content exceeds maximum length");

            var userId = tenant.CurrentUserId;
            try
            {
                var result = await mediator.Send(new AddNoteCommand(id, req.Content, userId));
                return Results.Created($"/api/deals/{id}/activities/{result.Id}", result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        group.MapPost("/deals/{id:guid}/activities", async (Guid id, [FromBody] LogActivityRequest req, IMediator mediator, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Type))
                return Results.BadRequest("Activity type is required");

            var userId = Guid.Parse(ctx.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
            var cmd = new LogActivityCommand(id, req.Type, req.Content, userId);
            try
            {
                var result = await mediator.Send(cmd);
                return Results.Created($"/api/deals/{id}/activities/{result.Id}", result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        group.MapGet("/contacts", async (string? search, DealsDbContext db) =>
        {
            if (!string.IsNullOrEmpty(search) && search.Length > 100)
                return Results.BadRequest("Search term too long");

            var query = db.Contacts.AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(c =>
                    EF.Functions.ILike(c.Name, $"%{search}%") ||
                    (c.Email != null && EF.Functions.ILike(c.Email, $"%{search}%")));

            return Results.Ok(await query.OrderBy(c => c.Name).Take(20).ToListAsync());
        });

        group.MapPost("/contacts", async ([FromBody] CreateContactRequest req, DealsDbContext db, ITenantContext tenant) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 200)
                return Results.BadRequest("Name is required and must be under 200 characters");

            var contact = new Domain.Contact { TenantId = tenant.Current, Name = req.Name, Email = req.Email, Company = req.Company, Phone = req.Phone };
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();
            return Results.Created($"/api/contacts/{contact.Id}", contact);
        });

        group.MapPost("/deals/{id:guid}/score:recalculate", async (
            Guid id, 
            PulseCRM.Shared.Proto.Scoring.ScoringService.ScoringServiceClient scoringClient, 
            ITenantContext tenant, 
            DealsDbContext db) =>
        {
            var dealExists = await db.Deals.AnyAsync(d => d.Id == id && d.TenantId == tenant.Current);
            if (!dealExists) return Results.NotFound();

            try
            {
                var response = await scoringClient.RecalculateScoreAsync(new PulseCRM.Shared.Proto.Scoring.RecalculateRequest
                {
                    TenantId = tenant.Current.ToString(),
                    DealId = id.ToString()
                });
                
                return Results.Ok(new 
                { 
                    Score = response.Score, 
                    Factors = response.Factors 
                });
            }
            catch (global::Grpc.Core.RpcException ex) when (ex.StatusCode == global::Grpc.Core.StatusCode.NotFound)
            {
                return Results.NotFound();
            }
        });
    }
}

// Request DTOs
public record UpdateDealRequest(string? Title, decimal? Value, Guid? OwnerId, Guid? ContactId, DateTimeOffset? ExpectedCloseDate);
public record MoveDealRequest(Guid StageId, int PositionInStage, uint RowVersion);
public record AddNoteRequest(string Content);
public record LogActivityRequest(string Type, string? Content);
public record CreateContactRequest(string Name, string? Email, string? Company, string? Phone);
