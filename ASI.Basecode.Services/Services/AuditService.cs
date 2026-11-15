using System.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    public sealed class AuditService : ServiceBase, IAuditService
    {
        private readonly IAuditRepository _auditRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AuditService(IAuditRepository audits, IUnitOfWork work)
        {
            _auditRepository = audits;
            _unitOfWork = work;
        }

        public async Task<AdminAuditPage> GetAuditAsync(string? q, int page = 1, CancellationToken cancellationToken = default)
        {
            q = SearchText(q);
            var data = await _auditRepository.ListAsync(q, page, cancellationToken);
            var events = data.Events;
            var total = data.Total;
            var pages = data.Pages;
            page = data.Page;
            var targets = await _auditRepository.GetTargetsAsync(events, cancellationToken);
            var rows = events
                .Select(x =>
                {
                    var described = AuditDescriptions.Describe(x, targets.GetValueOrDefault($"{x.EntityType}:{x.EntityId}"));
                    return new AdminAuditRow(
                        x.Id,
                        ManilaTime.FormatAudit(x.CreatedAtUtc),
                        described.Who,
                        described.WhoEmail,
                        described.WhatHappened,
                        described.Result,
                        described.ResultState
                    );
                })
                .ToList();
            return new AdminAuditPage(rows, q, page, pages, total);
        }

        private static string SearchText(string? value)
        {
            var text = value?.Trim() ?? string.Empty;
            return text.Length > 120 ? text[..120] : text;
        }

        public async Task RecordAccountAsync(RequestContext actor, string action, string outcome, string? userId, CancellationToken ct)
        {
            _auditRepository.Add(
                new AuditEvent
                {
                    ActorUserId = actor.UserId,
                    Action = action,
                    EntityType = "User",
                    EntityId = userId ?? "unknown",
                    Outcome = outcome,
                    IpAddress = actor.IpAddress,
                    CorrelationId = actor.CorrelationId,
                }
            );
            await _unitOfWork.SaveAsync(ct);
        }
    }
}
