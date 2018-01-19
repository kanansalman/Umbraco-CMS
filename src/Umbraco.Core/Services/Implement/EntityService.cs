﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Entities;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Persistence.Dtos;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Scoping;

namespace Umbraco.Core.Services.Implement
{
    public class EntityService : ScopeRepositoryService, IEntityService
    {
        private readonly IEntityRepository _entityRepository;
        private readonly Dictionary<string, (UmbracoObjectTypes ObjectType, Func<int, IUmbracoEntity> GetById, Func<Guid, IUmbracoEntity> GetByKey)> _objectTypes;
        private IQuery<IUmbracoEntity> _queryRootEntity;
        private readonly IdkMap _idkMap;

        public EntityService(IScopeProvider provider, ILogger logger, IEventMessagesFactory eventMessagesFactory,
           IContentService contentService, IContentTypeService contentTypeService,
           IMediaService mediaService, IMediaTypeService mediaTypeService,
           IDataTypeService dataTypeService,
           IMemberService memberService, IMemberTypeService memberTypeService, IdkMap idkMap,
           IEntityRepository entityRepository)
            : base(provider, logger, eventMessagesFactory)
        {
            _idkMap = idkMap;
            _entityRepository = entityRepository;

            _objectTypes = new Dictionary<string, (UmbracoObjectTypes, Func<int, IUmbracoEntity>, Func<Guid, IUmbracoEntity>)>
            {
                { typeof (IDataType).FullName, (UmbracoObjectTypes.DataType, dataTypeService.GetDataTypeDefinitionById, dataTypeService.GetDataTypeDefinitionById) },
                { typeof (IContent).FullName, (UmbracoObjectTypes.Document, contentService.GetById, contentService.GetById) },
                { typeof (IContentType).FullName, (UmbracoObjectTypes.DocumentType, contentTypeService.Get, contentTypeService.Get) },
                { typeof (IMedia).FullName, (UmbracoObjectTypes.Media, mediaService.GetById, mediaService.GetById) },
                { typeof (IMediaType).FullName, (UmbracoObjectTypes.MediaType, mediaTypeService.Get, mediaTypeService.Get) },
                { typeof (IMember).FullName, (UmbracoObjectTypes.Member, memberService.GetById, memberService.GetByKey) },
                { typeof (IMemberType).FullName, (UmbracoObjectTypes.MemberType, memberTypeService.Get, memberTypeService.Get) },
            };
        }

        #region Static Queries

        // lazy-constructed because when the ctor runs, the query factory may not be ready
        private IQuery<IUmbracoEntity> QueryRootEntity => _queryRootEntity
            ?? (_queryRootEntity = Query<IUmbracoEntity>().Where(x => x.ParentId == -1));

        #endregion

        // gets the getters, throws if not supported
        private (UmbracoObjectTypes ObjectType, Func<int, IUmbracoEntity> GetById, Func<Guid, IUmbracoEntity> GetByKey) GetGetters(Type type)
        {
            if (type?.FullName == null || !_objectTypes.TryGetValue(type.FullName, out var getters))
                throw new NotSupportedException($"Type \"{type?.FullName ?? "<null>"}\" is not supported here.");
            return getters;
        }

        /// <inheritdoc />
        public IEntitySlim Get(int id)
        {
            return (IEntitySlim) Get(id, false);
        }

        /// <inheritdoc />
        public IUmbracoEntity Get(int id, bool full)
        {
            if (!full)
            {
                // get the light entity
                using (ScopeProvider.CreateScope(autoComplete: true))
                {
                    return _entityRepository.Get(id);
                }
            }

            // get the full entity
            var objectType = GetObjectType(id);
            var entityType = objectType.GetClrType();
            var getters = GetGetters(entityType);
            return getters.GetById(id);
        }

        /// <inheritdoc />
        public IEntitySlim Get(Guid key)
        {
            return (IEntitySlim) Get(key, false);
        }

        /// <inheritdoc />
        public IUmbracoEntity Get(Guid key, bool full)
        {
            if (!full)
            {
                // get the light entity
                using (ScopeProvider.CreateScope(autoComplete: true))
                {
                    return _entityRepository.Get(key);
                }
            }

            // get the full entity
            var objectType = GetObjectType(key);
            var entityType = objectType.GetClrType();
            var getters = GetGetters(entityType);
            return getters.GetByKey(key);
        }

        /// <inheritdoc />
        public virtual IEntitySlim Get(int id, UmbracoObjectTypes objectType)
        {
            return (IEntitySlim) Get(id, objectType, false);
        }

        /// <inheritdoc />
        public virtual IUmbracoEntity Get(int id, UmbracoObjectTypes objectType, bool full)
        {
            if (!full)
            {
                // get the light entity
                using (ScopeProvider.CreateScope(autoComplete: true))
                {
                    return _entityRepository.Get(id, objectType.GetGuid());
                }
            }

            // get the full entity
            var entityType = objectType.GetClrType();
            var getters = GetGetters(entityType);
            return getters.GetById(id);
        }

        /// <inheritdoc />
        public IEntitySlim Get(Guid key, UmbracoObjectTypes objectType)
        {
            return (IEntitySlim) Get(key, objectType, false);
        }

        /// <inheritdoc />
        public IUmbracoEntity Get(Guid key, UmbracoObjectTypes objectType, bool full)
        {
            if (!full)
            {
                // get the light entity
                using (ScopeProvider.CreateScope(autoComplete: true))
                {
                    return _entityRepository.Get(key, objectType.GetGuid());
                }
            }

            // get the full entity
            var entityType = objectType.GetClrType();
            var getters = GetGetters(entityType);
            return getters.GetByKey(key);
        }

        /// <inheritdoc />
        public virtual IEntitySlim Get<T>(int id)
            where T : IUmbracoEntity
        {
            return (IEntitySlim) Get<T>(id, false);
        }

        /// <inheritdoc />
        public virtual IUmbracoEntity Get<T>(int id, bool full)
            where T : IUmbracoEntity
        {
            if (!full)
            {
                // get the light entity
                using (ScopeProvider.CreateScope(autoComplete: true))
                {
                    return _entityRepository.Get(id);
                }
            }

            // get the full entity
            var entityType = typeof (T);
            var getters = GetGetters(entityType);
            return getters.GetById(id);
        }

        /// <inheritdoc />
        public virtual IEntitySlim Get<T>(Guid key)
            where T : IUmbracoEntity
        {
            return (IEntitySlim) Get<T>(key, false);
        }

        /// <inheritdoc />
        public IUmbracoEntity Get<T>(Guid key, bool full)
            where T : IUmbracoEntity
        {
            if (!full)
            {
                // get the light entity
                using (ScopeProvider.CreateScope(autoComplete: true))
                {
                    return _entityRepository.Get(key);
                }
            }

            // get the full entity
            var entityType = typeof (T);
            var getters = GetGetters(entityType);
            return getters.GetByKey(key);
        }

        /// <inheritdoc />
        public bool Exists(int id)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.Exists(id);
            }
        }

        /// <inheritdoc />
        public bool Exists(Guid key)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.Exists(key);
            }
        }


        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetAll<T>() where T : IUmbracoEntity
            => GetAll<T>(Array.Empty<int>());

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetAll<T>(params int[] ids)
            where T : IUmbracoEntity
        {
            var entityType = typeof (T);
            var getters = GetGetters(entityType);
            var objectType = getters.ObjectType;
            var objectTypeId = objectType.GetGuid();

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetAll(objectTypeId, ids);
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetAll(UmbracoObjectTypes objectType)
            => GetAll(objectType, Array.Empty<int>());

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetAll(UmbracoObjectTypes objectType, params int[] ids)
        {
            var entityType = objectType.GetClrType();
            GetGetters(entityType);

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetAll(objectType.GetGuid(), ids);
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetAll(Guid objectType)
            => GetAll(objectType, Array.Empty<int>());

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetAll(Guid objectType, params int[] ids)
        {
            var entityType = ObjectTypes.GetClrType(objectType);
            GetGetters(entityType);

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetAll(objectType, ids);
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetAll<T>(params Guid[] keys)
            where T : IUmbracoEntity
        {
            var entityType = typeof (T);
            var getters = GetGetters(entityType);
            var objectType = getters.ObjectType;
            var objectTypeId = objectType.GetGuid();

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetAll(objectTypeId, keys);
            }
        }

        /// <inheritdoc />
        public IEnumerable<IEntitySlim> GetAll(UmbracoObjectTypes objectType, Guid[] keys)
        {
            var entityType = objectType.GetClrType();
            GetGetters(entityType);

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetAll(objectType.GetGuid(), keys);
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetAll(Guid objectType, params Guid[] keys)
        {
            var entityType = ObjectTypes.GetClrType(objectType);
            GetGetters(entityType);

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetAll(objectType, keys);
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetRootEntities(UmbracoObjectTypes objectType)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetByQuery(QueryRootEntity, objectType.GetGuid());
            }
        }

        /// <inheritdoc />
        public virtual IEntitySlim GetParent(int id)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var entity = _entityRepository.Get(id);
                if (entity.ParentId == -1 || entity.ParentId == -20 || entity.ParentId == -21)
                    return null;
                return _entityRepository.Get(entity.ParentId);
            }
        }

        /// <inheritdoc />
        public virtual IEntitySlim GetParent(int id, UmbracoObjectTypes objectType)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var entity = _entityRepository.Get(id);
                if (entity.ParentId == -1 || entity.ParentId == -20 || entity.ParentId == -21)
                    return null;
                return _entityRepository.Get(entity.ParentId, objectType.GetGuid());
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetChildren(int parentId)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var query = Query<IUmbracoEntity>().Where(x => x.ParentId == parentId);
                return _entityRepository.GetByQuery(query);
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetChildren(int parentId, UmbracoObjectTypes objectType)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var query = Query<IUmbracoEntity>().Where(x => x.ParentId == parentId);
                return _entityRepository.GetByQuery(query, objectType.GetGuid());
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetDescendants(int id)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var entity = _entityRepository.Get(id);
                var pathMatch = entity.Path + ",";
                var query = Query<IUmbracoEntity>().Where(x => x.Path.StartsWith(pathMatch) && x.Id != id);
                return _entityRepository.GetByQuery(query);
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IEntitySlim> GetDescendants(int id, UmbracoObjectTypes objectType)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var entity = _entityRepository.Get(id);
                var query = Query<IUmbracoEntity>().Where(x => x.Path.StartsWith(entity.Path) && x.Id != id);
                return _entityRepository.GetByQuery(query, objectType.GetGuid());
            }
        }

        /// <inheritdoc />
        public IEnumerable<IEntitySlim> GetPagedChildren(int id, UmbracoObjectTypes objectType, long pageIndex, int pageSize, out long totalRecords,
            string orderBy = "SortOrder", Direction orderDirection = Direction.Ascending, string filter = "")
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var query = Query<IUmbracoEntity>().Where(x => x.ParentId == id && x.Trashed == false);

                var filterQuery = string.IsNullOrWhiteSpace(filter) ? null : Query<IUmbracoEntity>().Where(x => x.Name.Contains(filter));
                return _entityRepository.GetPagedResultsByQuery(query, objectType.GetGuid(), pageIndex, pageSize, out totalRecords, orderBy, orderDirection, filterQuery);
            }
        }

        /// <inheritdoc />
        public IEnumerable<IEntitySlim> GetPagedDescendants(int id, UmbracoObjectTypes objectType, long pageIndex, int pageSize, out long totalRecords,
            string orderBy = "path", Direction orderDirection = Direction.Ascending, string filter = "")
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var objectTypeGuid = objectType.GetGuid();
                var query = Query<IUmbracoEntity>();

                if (id != Constants.System.Root)
                {
                    // lookup the path so we can use it in the prefix query below
                    var paths = _entityRepository.GetAllPaths(objectTypeGuid, id).ToArray();
                    if (paths.Length == 0)
                    {
                        totalRecords = 0;
                        return Enumerable.Empty<IEntitySlim>();
                    }
                    var path = paths[0].Path;
                    query.Where(x => x.Path.SqlStartsWith(path + ",", TextColumnType.NVarchar));
                }

                var filterQuery = string.IsNullOrWhiteSpace(filter) ? null : Query<IUmbracoEntity>().Where(x => x.Name.Contains(filter));
                return _entityRepository.GetPagedResultsByQuery(query, objectTypeGuid, pageIndex, pageSize, out totalRecords, orderBy, orderDirection, filterQuery);
            }
        }

        /// <inheritdoc />
        public IEnumerable<IEntitySlim> GetPagedDescendants(IEnumerable<int> ids, UmbracoObjectTypes objectType, long pageIndex, int pageSize, out long totalRecords,
            string orderBy = "path", Direction orderDirection = Direction.Ascending, string filter = "")
        {
            totalRecords = 0;

            var idsA = ids.ToArray();
            if (idsA.Length == 0)
                return Enumerable.Empty<IEntitySlim>();

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var objectTypeGuid = objectType.GetGuid();
                var query = Query<IUmbracoEntity>();

                if (idsA.All(x => x != Constants.System.Root))
                {
                    var paths = _entityRepository.GetAllPaths(objectTypeGuid, idsA).ToArray();
                    if (paths.Length == 0)
                    {
                        totalRecords = 0;
                        return Enumerable.Empty<IEntitySlim>();
                    }
                    var clauses = new List<Expression<Func<IUmbracoEntity, bool>>>();
                    foreach (var id in idsA)
                    {
                        // if the id is root then don't add any clauses
                        if (id == Constants.System.Root) continue;

                        var entityPath = paths.FirstOrDefault(x => x.Id == id);
                        if (entityPath == null) continue;

                        var path = entityPath.Path;
                        var qid = id;
                        clauses.Add(x => x.Path.SqlStartsWith(path + ",", TextColumnType.NVarchar) || x.Path.SqlEndsWith("," + qid, TextColumnType.NVarchar));
                    }
                    query.WhereAny(clauses);
                }

                var filterQuery = string.IsNullOrWhiteSpace(filter) ? null : Query<IUmbracoEntity>().Where(x => x.Name.Contains(filter));
                return _entityRepository.GetPagedResultsByQuery(query, objectTypeGuid, pageIndex, pageSize, out totalRecords, orderBy, orderDirection, filterQuery);
            }
        }

        /// <inheritdoc />
        public IEnumerable<IEntitySlim> GetPagedDescendants(UmbracoObjectTypes objectType, long pageIndex, int pageSize, out long totalRecords,
            string orderBy = "path", Direction orderDirection = Direction.Ascending, string filter = "", bool includeTrashed = true)
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                var query = Query<IUmbracoEntity>();
                if (includeTrashed == false)
                    query.Where(x => x.Trashed == false);

                var filterQuery = string.IsNullOrWhiteSpace(filter) ? null : Query<IUmbracoEntity>().Where(x => x.Name.Contains(filter));
                return _entityRepository.GetPagedResultsByQuery(query, objectType.GetGuid(), pageIndex, pageSize, out totalRecords, orderBy, orderDirection, filterQuery);
            }
        }

        /// <inheritdoc />
        public virtual UmbracoObjectTypes GetObjectType(int id)
        {
            using (var scope = ScopeProvider.CreateScope(autoComplete: true))
            {
                var sql = scope.SqlContext.Sql()
                    .Select<NodeDto>(x => x.NodeObjectType)
                    .From<NodeDto>()
                    .Where<NodeDto>(x => x.NodeId == id);

                var guid = scope.Database.ExecuteScalar<Guid>(sql);
                return ObjectTypes.GetUmbracoObjectType(guid);
            }
        }

        /// <inheritdoc />
        public virtual UmbracoObjectTypes GetObjectType(Guid key)
        {
            using (var scope = ScopeProvider.CreateScope(autoComplete: true))
            {
                var sql = scope.SqlContext.Sql()
                    .Select<NodeDto>(x => x.NodeObjectType)
                    .From<NodeDto>()
                    .Where<NodeDto>(x => x.UniqueId == key);

                var guid = scope.Database.ExecuteScalar<Guid>(sql);
                return ObjectTypes.GetUmbracoObjectType(guid);
            }
        }

        /// <inheritdoc />
        public virtual UmbracoObjectTypes GetObjectType(IUmbracoEntity entity)
        {
            return entity is IEntitySlim light
                ? ObjectTypes.GetUmbracoObjectType(light.NodeObjectType)
                : GetObjectType(entity.Id);
        }

        /// <inheritdoc />
        public virtual Type GetEntityType(int id)
        {
            var objectType = GetObjectType(id);
            return objectType.GetClrType();
        }

        /// <inheritdoc />
        public Attempt<int> GetId(Guid key, UmbracoObjectTypes objectType)
        {
            return _idkMap.GetIdForKey(key, objectType);
        }

        /// <inheritdoc />
        public Attempt<int> GetId(Udi udi)
        {
            return _idkMap.GetIdForUdi(udi);
        }

        /// <inheritdoc />
        public Attempt<Guid> GetKey(int id, UmbracoObjectTypes umbracoObjectType)
        {
            return _idkMap.GetKeyForId(id, umbracoObjectType);
        }

        /// <inheritdoc />
        public virtual IEnumerable<TreeEntityPath> GetAllPaths(UmbracoObjectTypes objectType, params int[] ids)
        {
            var entityType = objectType.GetClrType();
            GetGetters(entityType);

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetAllPaths(objectType.GetGuid(), ids);
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<TreeEntityPath> GetAllPaths(UmbracoObjectTypes objectType, params Guid[] keys)
        {
            var entityType = objectType.GetClrType();
            GetGetters(entityType);

            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return _entityRepository.GetAllPaths(objectType.GetGuid(), keys);
            }
        }

        /// <inheritdoc />
        public int ReserveId(Guid key)
        {
            NodeDto node;
            using (var scope = ScopeProvider.CreateScope())
            {
                var sql = scope.SqlContext.Sql()
                    .Select<NodeDto>()
                    .From<NodeDto>()
                    .Where<NodeDto>(x => x.UniqueId == key && x.NodeObjectType == Constants.ObjectTypes.IdReservation);

                node = scope.Database.SingleOrDefault<NodeDto>(sql);
                if (node != null)
                    throw new InvalidOperationException("An identifier has already been reserved for this Udi.");

                node = new NodeDto
                {
                    UniqueId = key,
                    Text = "RESERVED.ID",
                    NodeObjectType = Constants.ObjectTypes.IdReservation,

                    CreateDate = DateTime.Now,
                    UserId = 0,
                    ParentId = -1,
                    Level = 1,
                    Path = "-1",
                    SortOrder = 0,
                    Trashed = false
                };
                scope.Database.Insert(node);
                scope.Complete();
            }
            return node.NodeId;
        }
    }
}