using System;
using System.Collections.Generic;
using System.Text;

namespace PowerShellEditorServices.Hosting
{
    public class ProfilePathConfig
    {
        public ProfilePathConfig(string allUsersPath, string currentUserPath)
        {
            AllUsersProfilePath = allUsersPath;
            CurrentUserProfilePath = currentUserPath;
        }

        public string AllUsersProfilePath { get; }

        public string CurrentUserProfilePath { get; }
    }
}
