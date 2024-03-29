﻿namespace Negwork.Data.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Common;
    using Common.Constants;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;

    public class User : IdentityUser
    {
        private ICollection<Article> articles;
        private ICollection<Image> images;
        private ICollection<Rating> ratings;
        private ICollection<Vote> votes;

        public User()
        {
            this.articles = new HashSet<Article>();
            this.images = new HashSet<Image>();
            this.ratings = new HashSet<Rating>();
            this.votes = new HashSet<Vote>();
        }

        [Required]
        [MinLength(ModelConstants.MIN_USER_FIRSTNAME_LENGTH)]
        [MaxLength(ModelConstants.MAX_USER_FIRSTNAME_LENGTH)]
        public string FirstName { get; set; }

        [Required]
        [MinLength(ModelConstants.MIN_USER_LASTNAME_LENGTH)]
        [MaxLength(ModelConstants.MAX_USER_LASTNAME_LENGTH)]
        public string LastName { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [Required]
        public Gender Gender { get; set; }
    
        [MaxLength(ModelConstants.MAX_USER_ADDITIONAL_INFO_LENGTH)]
        public string AdditionalInfo { get; set; }

        [Url]
        public string ProfileImage { get; set; }

        public virtual ICollection<Article> Articles
        {
            get
            {
                return this.articles;
            }

            set
            {
                this.articles = value;
            }
        }

        public virtual ICollection<Image> Images
        {
            get
            {
                return this.images;
            }

            set
            {
                this.images = value;
            }
        }

        public virtual ICollection<Vote> Votes
        {
            get
            {
                return this.votes;
            }

            set
            {
                this.votes = value;
            }
        }

        public virtual ICollection<Rating> Ratings
        {
            get
            {
                return this.ratings;
            }

            set
            {
                this.ratings = value;
            }
        }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<User> manager, string authenticationType)
        {
            //// Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, authenticationType);
            //// Add custom user claims here
            return userIdentity;
        }
    }
}
