using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using VacationManager.Core.Services.Abstractions;
using VacationManager.Data.Models.Abstractions;
using VacationManager.Data.Repositories.Abstractions;

namespace VacationManager.Core.Services;

public abstract class BaseService<TEntity, TPrototype>(IRepository<TEntity> repository) : IService<TEntity, TPrototype>
    where TEntity : class, IEntity, new()
    where TPrototype : class
{
    protected readonly IRepository<TEntity> Repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public virtual Task<TEntity[]> GetAllAsync(CancellationToken cancellationToken)
        => this.Repository.GetManyAsync(this.BuildAdditionalFilters(), cancellationToken);

    public Task<TEntity[]> GetAllWithNavigationsAsync(IEnumerable<string> navigations, CancellationToken cancellationToken)
        => this.Repository.GetManyWithNavigationsAsync(this.BuildAdditionalFilters(), navigations, cancellationToken);

    public Task<TEntity[]> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
        => this.Repository.GetManyAsync([e => ids.Contains(e.Id), .. this.BuildAdditionalFilters()], cancellationToken);

    public virtual Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => this.Repository.GetAsync([e => e.Id == id, .. this.BuildAdditionalFilters()], cancellationToken);

    public Task<TEntity?> GetByIdCompleteAsync(Guid id, CancellationToken cancellationToken)
        => this.Repository.GetCompleteAsync([e => e.Id == id, .. this.BuildAdditionalFilters()], cancellationToken);

    public Task<TEntity?> GetByIdWithNavigationsAsync(Guid id, IEnumerable<string> navigations, CancellationToken cancellationToken)
        => this.Repository.GetWithNavigationsAsync([e => e.Id == id, .. this.BuildAdditionalFilters()], navigations, cancellationToken);

    public async Task<TEntity> GetByIdRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        TEntity? entity = await this.Repository.GetAsync([e => e.Id == id, .. this.BuildAdditionalFilters()], cancellationToken);
        EnsureEntityNotNull(entity);

        return entity;
    }

    public async Task<TEntity> CreateAsync(TPrototype prototype, CancellationToken cancellationToken)
    {
        TEntity entity = await this.InitializeAsync(prototype, cancellationToken);
        await this.ApplyAsync(entity, prototype, cancellationToken);

        DateTime currentTime = DateTime.Now;
        entity.CreatedAt = currentTime;
        entity.LastModifiedAt = currentTime;

        await this.Repository.CreateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<TEntity> UpdateAsync(Guid entityId, TPrototype prototype, CancellationToken cancellationToken)
    {
        TEntity? entity = await this.GetByIdCompleteAsync(entityId, cancellationToken);
        EnsureEntityNotNull(entity);

        await this.ApplyAsync(entity, prototype, cancellationToken);
        entity.LastModifiedAt = DateTime.Now;

        await this.Repository.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    public virtual async Task SoftDeleteAsync(Guid entityId, CancellationToken cancellationToken)
    {
        TEntity entity = await this.GetByIdRequiredAsync(entityId, cancellationToken);

        entity.IsDeleted = true;
        entity.LastModifiedAt = DateTime.Now;
        await this.Repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task HardDeleteAsync(Guid entityId, CancellationToken cancellationToken)
    {
        TEntity entity = await this.GetByIdRequiredAsync(entityId, cancellationToken);

        await this.Repository.DeleteAsync(entity, cancellationToken);
    }

    protected abstract Task ApplyAsync(TEntity entity, TPrototype prototype, CancellationToken cancellationToken);

    protected virtual Task<TEntity> InitializeAsync(TPrototype prototype, CancellationToken cancellationToken) => Task.FromResult(new TEntity());
    protected virtual IEnumerable<Expression<Func<TEntity, bool>>> BuildAdditionalFilters() => [];

    private static void EnsureEntityNotNull([NotNull] TEntity? entity)
    {
        if (entity is null) throw new InvalidOperationException("Entity was not found.");
    }
}
