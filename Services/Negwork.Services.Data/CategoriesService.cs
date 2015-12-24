﻿namespace Negwork.Services.Data
{
    using System;
    using System.Linq;
    using Negwork.Data.Models;
    using Contracts;
    using Negwork.Data.Repositories;

    public class CategoriesService : ICategoriesService
    {
        private IRepository<Category> categories;

        public CategoriesService(IRepository<Category> categories)
        {
            this.categories = categories;
        }

        public IQueryable<Category> GetAll()
        {
            return this.categories.All();
        }

        public IQueryable<Category> GetArticleCategories()
        {
            return this.categories
                .All()
                .Where(c => c.Articles.Count > 0);
        }

        public IQueryable<Category> GetImageCategories()
        {
            return this.categories
                .All()
                .Where(c => c.Images.Count > 0);
        }

        public Category GetById(int id)
        {
            return this.categories.All()
                .Where(c => c.Id == id)
                .FirstOrDefault();
        }

        public Category CreateCategory(string name)
        {
            var category = new Category()
            {
                Name = name
            };

            categories.Add(category);
            categories.SaveChanges();

            return category;
        }
    }
}
