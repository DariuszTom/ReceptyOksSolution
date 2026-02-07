using ReceptyOks.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReceptyOks.Services
{
    internal class UserService
    {
        private User _user;
        public static readonly Lazy<UserService> Instance = new Lazy<UserService>(() => new UserService(), LazyThreadSafetyMode.ExecutionAndPublication);
        private UserService() { }

        public User GetUser()
        {
            return _user;
        }

        public void SetUser(User user)
        {
            _user = user;
        }   
    }
}
