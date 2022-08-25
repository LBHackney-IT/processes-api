// using Hackney.Core.JWT;
// using Hackney.Core.Sns;
// using Hackney.Shared.Tenure.Infrastructure;
// using System;
// using ProcessesApi.V2.Infrastructure;
//
// namespace ProcessesApi.V2.Factories
// {
//     public class TenureSnsFactory : ITenureSnsFactory
//     {
//         public EntityEventSns CreateTenure(TenureInformationDb tenure, Token token)
//         {
//             return new EntityEventSns
//             {
//                 CorrelationId = Guid.NewGuid(),
//                 DateTime = DateTime.UtcNow,
//                 EntityId = tenure.Id,
//                 Id = Guid.NewGuid(),
//                 EventType = TenureEventConstants.TENURE_CREATED_EVENT,
//                 Version = TenureEventConstants.V1_VERSION,
//                 SourceDomain = TenureEventConstants.SOURCE_DOMAIN,
//                 SourceSystem = TenureEventConstants.SOURCE_SYSTEM,
//                 EventData = new EventData
//                 {
//                     NewData = tenure
//                 },
//                 User = new User { Name = token.Name, Email = token.Email }
//             };
//         }
//
//         public EntityEventSns UpdateTenure(UpdateEntityResult<TenureInformationDb> updateResult, Token token)
//         {
//             return new EntityEventSns
//             {
//                 CorrelationId = Guid.NewGuid(),
//                 DateTime = DateTime.UtcNow,
//                 EntityId = updateResult.UpdatedEntity.Id,
//                 Id = Guid.NewGuid(),
//                 EventType = TenureEventConstants.TENURE_UPDATED_EVENT,
//                 Version = TenureEventConstants.V1_VERSION,
//                 SourceDomain = TenureEventConstants.SOURCE_DOMAIN,
//                 SourceSystem = TenureEventConstants.SOURCE_SYSTEM,
//                 EventData = new EventData
//                 {
//                     NewData = updateResult.NewValues,
//                     OldData = updateResult.OldValues
//                 },
//                 User = new User { Name = token.Name, Email = token.Email }
//             };
//         }
//
//         public EntityEventSns PersonAddedToTenure(UpdateEntityResult<TenureInformationDb> updateResult, Token token)
//         {
//             return new EntityEventSns
//             {
//                 CorrelationId = Guid.NewGuid(),
//                 DateTime = DateTime.UtcNow,
//                 EntityId = updateResult.UpdatedEntity.Id,
//                 Id = Guid.NewGuid(),
//                 EventType = TenureEventConstants.PERSON_ADDED_TO_TENURE_EVENT,
//                 Version = TenureEventConstants.V1_VERSION,
//                 SourceDomain = TenureEventConstants.SOURCE_DOMAIN,
//                 SourceSystem = TenureEventConstants.SOURCE_SYSTEM,
//                 EventData = new EventData
//                 {
//                     NewData = updateResult.NewValues,
//                     OldData = updateResult.OldValues
//                 },
//                 User = new User { Name = token.Name, Email = token.Email }
//             };
//         }
//
//         public EntityEventSns PersonRemovedFromTenure(UpdateEntityResult<TenureInformationDb> updateResult, Token token)
//         {
//             return new EntityEventSns
//             {
//                 CorrelationId = Guid.NewGuid(),
//                 DateTime = DateTime.UtcNow,
//                 EntityId = updateResult.UpdatedEntity.Id,
//                 Id = Guid.NewGuid(),
//                 EventType = TenureEventConstants.PERSON_REMOVED_FROM_TENURE_EVENT,
//                 Version = TenureEventConstants.V1_VERSION,
//                 SourceDomain = TenureEventConstants.SOURCE_DOMAIN,
//                 SourceSystem = TenureEventConstants.SOURCE_SYSTEM,
//                 EventData = new EventData
//                 {
//                     NewData = updateResult.NewValues,
//                     OldData = updateResult.OldValues
//                 },
//                 User = new User { Name = token.Name, Email = token.Email }
//             };
//         }
//     }
// }
