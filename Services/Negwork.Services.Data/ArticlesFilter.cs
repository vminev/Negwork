﻿namespace Negwork.Services.Data
{
    using Negwork.Data.Models;
    using Common;
    using System.Linq;

    public static class QueryableExtensions
    {
        public static IQueryable<Article> FilterArticles(this IQueryable<Article> query, ArticleFilterModel filters)
        {
            if (filters == null)
            {
                return query;
            }

            if (filters.Category != null)
            {
                query = query.Where(a => a.Category.Name == filters.Category);
            }

            query = query.OrderByDescending(a => a.DatePublished);

            if (filters.OrderType == "asc")
            {
                if (filters.OrderBy == "date")
                {
                    query = query.OrderBy(a => a.DatePublished);
                }

                if (filters.OrderBy == "rating")
                {
                    query = query.OrderBy(a => a.NumberOfRatings != 0 ? ((double)a.AllRatings / (double)a.NumberOfRatings) : 0);
                }

                if (filters.OrderBy == "userrating")
                {
                    // TODO: Extend
                    //query = query.OrderBy(a => (d))
                }

                if (filters.OrderBy == "title")
                {
                    query = query.OrderBy(a => a.Title);
                }

                if (filters.OrderBy == "category")
                {
                    query = query.OrderBy(a => a.Category);
                }
            }
            else
            {
                if (filters.OrderBy == "date")
                {
                    query = query.OrderByDescending(a => a.DatePublished);
                }

                if (filters.OrderBy == "rating")
                {
                    query = query.OrderByDescending(a => a.NumberOfRatings != 0 ? ((double)a.AllRatings / (double)a.NumberOfRatings) : 0);
                }

                if (filters.OrderBy == "userrating")
                {
                    // TODO: Extend
                    //query = query.OrderBy(a => (d))
                }

                if (filters.OrderBy == "title")
                {
                    query = query.OrderByDescending(a => a.Title);
                }

                if (filters.OrderBy == "category")
                {
                    query = query.OrderByDescending(a => a.Category);
                }
            }

            if (filters.Filter != null)
            {
                if (filters.FilterBy == "title")
                {
                    query = query.Where(a => a.Title.Contains(filters.Filter));
                }

                if (filters.FilterBy == "user")
                {
                    query = query.Where(a => a.Author.UserName.Contains(filters.Filter));
                }

                if (filters.FilterBy == "description")
                {
                    query = query.Where(a => a.Description.Contains(filters.Filter));
                }
            }

            int size = filters.PageSize;
            int page = filters.Page;

            query = query
                .Skip((page - 1) * size)
                .Take(size);

            return query;
        }
    }
}