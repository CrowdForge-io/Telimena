﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using Telimena.WebApp.Core.Interfaces;
using Telimena.WebApp.Core.Models;
using Telimena.WebApp.Core.Models.Portal;
using Telimena.WebApp.Infrastructure.Database;
using Telimena.WebApp.Infrastructure.Identity;
using Telimena.WebApp.Infrastructure.Repository;
using Telimena.WebApp.Infrastructure.Repository.Implementation;

namespace Telimena.WebApp.Infrastructure.UnitOfWork.Implementation
{
    public class AccountUnitOfWork : IAccountUnitOfWork
    {
        public AccountUnitOfWork(IAuthenticationManager authManager, ITelimenaUserManager userManager, TelimenaPortalContext context)
        {
            this.AuthManager = authManager;
            this.UserManager = userManager;
            this._context = context;
            this.DeveloperRepository = new Repository<DeveloperTeam>(context);
        }

        private readonly TelimenaPortalContext _context;

        public IAuthenticationManager AuthManager { get; }
        public ITelimenaUserManager UserManager { get; }
        public IRepository<DeveloperTeam> DeveloperRepository { get; }

        public int Complete()
        {
            return this._context.SaveChanges();
        }

        public async Task<Tuple<IdentityResult, IdentityResult>> RegisterUserAsync(TelimenaUser user, string password, params string[] roles)
        {
            IdentityResult registerResult = await this.UserManager.CreateAsync(user, password).ConfigureAwait(false);
            if (registerResult.Succeeded)
            {
                TelimenaUser addedUser = await this.UserManager.FindByIdAsync(user.Id).ConfigureAwait(false);
                addedUser.IsActivated = true;
                IdentityResult roleResult = await this.HandleRoleRegistrationAsync(roles, addedUser).ConfigureAwait(false);

                return new Tuple<IdentityResult, IdentityResult>(registerResult, roleResult);
            }

            return new Tuple<IdentityResult, IdentityResult>(registerResult, null);
        }

        public Task CompleteAsync()
        {
            return this._context.SaveChangesAsync();
        }

        private async Task<IdentityResult> HandleRoleRegistrationAsync(string[] roles, TelimenaUser user)
        {
            IdentityResult roleresult = IdentityResult.Success;
            if (roles.Contains(TelimenaRoles.Viewer))
            {
                roleresult = await this.UserManager.AddToRoleAsync(user.Id, TelimenaRoles.Viewer).ConfigureAwait(false);
                if (!roleresult.Succeeded)
                {
                    return roleresult;
                }
            }


            if (roles.Contains(TelimenaRoles.Developer))
            {
                roleresult = await this.UserManager.AddToRoleAsync(user.Id, TelimenaRoles.Developer).ConfigureAwait(false);
                if (roleresult.Succeeded)
                {
                    DeveloperTeam developer = new DeveloperTeam(user);
                    this.DeveloperRepository.Add(developer);
                    this._context.Users.Attach(user);
                }
            }

            return roleresult;
        }
    }
}