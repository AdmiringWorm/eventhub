using System;
using System.Linq;
using System.Threading.Tasks;
using EventHub.Emailing;
using EventHub.Organizations.Memberships;
using EventHub.Users;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Linq;
using Volo.Abp.TextTemplating;

namespace EventHub.Events
{
    public class NewEventNotifier : ITransientDependency
    {
        private readonly IEmailSender _emailSender;
        private readonly ITemplateRenderer _templateRenderer;
        private readonly IRepository<OrganizationMembership, Guid>  _organizationMembershipsRepository;
        private readonly IRepository<AppUser, Guid> _userRepository;
        private readonly IAsyncQueryableExecuter _asyncExecuter;

        public NewEventNotifier(
            IEmailSender emailSender, 
            ITemplateRenderer templateRenderer,
            IRepository<OrganizationMembership, Guid> organizationMembershipsRepository, 
            IRepository<AppUser, Guid> userRepository, 
            IAsyncQueryableExecuter asyncExecuter)
        {
            _emailSender = emailSender;
            _templateRenderer = templateRenderer;
            _organizationMembershipsRepository = organizationMembershipsRepository;
            _userRepository = userRepository;
            _asyncExecuter = asyncExecuter;
        }
        
        public async Task NotifyAsync(Event @event)
        {
            if (@event is null)
            {
                return;
            }

            var organizationMembershipsQueryable = await _organizationMembershipsRepository.GetQueryableAsync();
            
            var membershipQuery = organizationMembershipsQueryable
                .Where(x => x.OrganizationId == @event.OrganizationId);
            
            var organizationMembers = await _asyncExecuter.ToListAsync(membershipQuery);

            foreach (var member in organizationMembers)
            {
                var user = await _userRepository.FindAsync(member.UserId);
                
                if (user is null)
                {
                    continue;
                }

                var model = new
                {
                    UserName = user.GetFullNameOrUsername(),
                    Title = @event.Title,
                    StartTime = @event.StartTime,
                    EndTime = @event.EndTime,
                    Url = @event.Url
                };
                
                await _emailSender.QueueAsync(
                    user.Email,
                    $"Do you attend the {@event.Title} event?",
                    await _templateRenderer.RenderAsync(EmailTemplates.NewEventCreated, model)
                );      
            }
        }
    }
}