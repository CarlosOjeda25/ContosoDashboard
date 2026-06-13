using ContosoDashboard.Services.Documents;
using MediatR;

namespace ContosoDashboard.Services.Documents.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetMyDocumentsQuery(
    int UserId,
    DocumentFilter Filter
) : IRequest<PagedResult<DocumentSummary>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetMyDocumentsQueryHandler
    : IRequestHandler<GetMyDocumentsQuery, PagedResult<DocumentSummary>>
{
    private readonly IDocumentRepository _documents;

    public GetMyDocumentsQueryHandler(IDocumentRepository documents)
        => _documents = documents;

    public async Task<PagedResult<DocumentSummary>> Handle(
        GetMyDocumentsQuery query, CancellationToken ct)
    {
        // Ensure the filter is scoped to the requesting user
        var filter = query.Filter with { UserId = query.UserId };
        return await _documents.GetPagedAsync(filter, ct);
    }
}
