
using AutoMapper;
using Ecom.Core.DTO;
using Ecom.Core.Entities.Product;
using Ecom.Core.interfaces;
using Ecom.Core.Services;
using Ecom.Core.Sharing;


using Ecom.infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Ecom.infrastructure.Repositries
{
    public class ProductRepositry : GenericRepositry<Product>, IProductRepositry
    {
        private readonly AppDbContext context;
        private readonly IMapper mapper;
        private readonly IImageManagementService imageManagementService;
        public ProductRepositry(AppDbContext context, IMapper mapper, IImageManagementService imageManagementService) : base(context)
        {
            this.context = context;
            this.mapper = mapper;
            this.imageManagementService = imageManagementService;
        }
        //public async Task<IEnumerable<ProductDTO>> GetAllAsync(ProductParams productParams)
        //{
        //    var query = context.Products
        //        .Include(m => m.Category)
        //        .Include(m => m.Photos)
        //        .AsNoTracking();



        //    //filtering by word
        //    if (!string.IsNullOrEmpty(productParams.Search))
        //    {
        //        var searchWords = productParams.Search.Split(' ');
        //        query = query.Where(m => searchWords.All(word =>

        //        m.Name.ToLower().Contains(word.ToLower()) ||
        //        m.Description.ToLower().Contains(word.ToLower())

        //        ));
        //    }



        //    //filtering by category Id
        //    if (productParams.CategoryId.HasValue)
        //        query = query.Where(m => m.CategoryId == productParams.CategoryId);

        //    if (!string.IsNullOrEmpty(productParams.Sort))
        //    {
        //        query = productParams.Sort switch
        //        {
        //            "PriceAce" => query.OrderBy(m => m.NewPrice),
        //            "PriceDce" => query.OrderByDescending(m => m.NewPrice),
        //            _ => query.OrderBy(m => m.Name),
        //        };
        //    }

        //    productParams.TotatlCount = query.Count();

        //    query = query.Skip((productParams.pageSize) * (productParams.PageNumber - 1)).Take(productParams.pageSize);


        //    var result = mapper.Map<List<ProductDTO>>(query);

        //    return result;

        //}
        public async Task<IEnumerable<ProductDTO>> GetAllAsync(ProductParams productParams)
        {
            var query = context.Products
                .Include(m => m.Category)
                .Include(m => m.Photos)
                .AsNoTracking();

            // فلترة البحث - تعمل فقط عند وجود نص بحث
            if (!string.IsNullOrEmpty(productParams.Search))
            {
                var searchTerm = productParams.Search.Trim().ToLower();
                query = query.Where(m =>
                    m.Name.ToLower().Contains(searchTerm) ||
                    m.Description.ToLower().Contains(searchTerm));
            }

            // فلترة حسب الفئة
            if (productParams.CategoryId.HasValue)
            {
                query = query.Where(m => m.CategoryId == productParams.CategoryId);
            }

            // الترتيب
            query = productParams.Sort switch
            {
                "PriceAce" => query.OrderBy(m => m.NewPrice),
                "PriceDce" => query.OrderByDescending(m => m.NewPrice),
                _ => query.OrderBy(m => m.Name) // افتراضي
            };

            // حساب العدد الكلي قبل الترقيم
            productParams.TotalCount = await query.CountAsync();

            // تطبيق الترقيم
            var pagedQuery = query
                .Skip((productParams.PageNumber - 1) * productParams.PageSize)
                .Take(productParams.PageSize);

            return await mapper.ProjectTo<ProductDTO>(pagedQuery).ToListAsync();
        }
        //public async Task<bool> AddAsync(AddProductDTO productDTO)
        //{
        //    if (productDTO == null) return false;

        //    var product = mapper.Map<Product>(productDTO);

        //    await context.Products.AddAsync(product);
        //    await context.SaveChangesAsync();

        //    var ImagePath = await imageManagementService.AddImageAsync(productDTO.Photo, productDTO.Name);

        //    var photo = ImagePath.Select(path => new Photo
        //    {
        //        ImageName = path,
        //        ProductId = product.Id,
        //    }).ToList();
        //    await context.Photos.AddRangeAsync(photo);
        //    await context.SaveChangesAsync();
        //    return true;
        //}

        public async Task<bool> AddAsync(AddProductDTO productDTO)
        {
            // Early null check
            if (productDTO?.Photo == null || string.IsNullOrEmpty(productDTO.Name))
            {
                return false;
                // Or consider throwing ArgumentException with specific message
            }

            try
            {
                var product = mapper.Map<Product>(productDTO);
                if (product == null) return false; // Handle mapping failure

                await context.Products.AddAsync(product);
                await context.SaveChangesAsync();

                // Handle potential null from service
                var imagePaths = await imageManagementService.AddImageAsync(productDTO.Photo, productDTO.Name);
                if (imagePaths == null || !imagePaths.Any())
                {
                    // Option 1: Rollback product creation
                    // context.Products.Remove(product);
                    // await context.SaveChangesAsync();

                    // Option 2: Continue without images
                    return true;
                }

                var photos = imagePaths
                    .Where(path => !string.IsNullOrEmpty(path)) // Additional safety
                    .Select(path => new Photo
                    {
                        ImageName = path,
                        ProductId = product.Id,
                    }).ToList();

                if (photos.Any())
                {
                    await context.Photos.AddRangeAsync(photos);
                    await context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex) // Consider more specific exception handling
            {
                // Log the exception
                return false;
            }
        }
        public async Task<bool> UpdateAsync(UpdateProductDTO updateProductDTO)
        {
            if (updateProductDTO is null)
            {
                return false;
            }
            var FindProduct = await context.Products.Include(m => m.Category)
                .Include(m => m.Photos)
                .FirstOrDefaultAsync(m => m.Id == updateProductDTO.Id);

            if (FindProduct is null)
            {
                return false;
            }
            mapper.Map(updateProductDTO, FindProduct);

            var FindPhoto = await context.Photos.Where(m => m.ProductId == updateProductDTO.Id).ToListAsync();

            foreach (var item in FindPhoto)
            {
                imageManagementService.DeleteImageAsync(item.ImageName);
            }
            context.Photos.RemoveRange(FindPhoto);

            var ImagePath = await imageManagementService.AddImageAsync(updateProductDTO.Photo, updateProductDTO.Name);

            var photo = ImagePath.Select(path => new Photo
            {
                ImageName = path,
                ProductId = updateProductDTO.Id,
            }).ToList();

            await context.Photos.AddRangeAsync(photo);

            await context.SaveChangesAsync();
            return true;

        }

        public async Task DeleteAsync(Product product)
        {
            var photo = await context.Photos.Where(m => m.ProductId == product.Id)
            .ToListAsync();
            foreach (var item in photo)
            {
                imageManagementService.DeleteImageAsync(item.ImageName);
            }
            context.Products.Remove(product);
            await context.SaveChangesAsync();
        }



    }
}





































//public async Task<bool> UpdateAsync(UpdateProductDTO productResponse)
//{
//    if (productResponse is null)
//    {
//        return false;
//    }

//    var findProduct = await context.Products
//        .Include(x => x.Category)
//        .Include(x => x.Photos)
//        .FirstOrDefaultAsync(i => i.Id == productResponse.Id);

//    if (findProduct == null)
//    {
//        return false;
//    }

//    // Update the existing entity's properties using AutoMapper or manually
//    mapper.Map(productResponse, findProduct);

//    if (productResponse.Photo is not null)
//    {
//        var photos = await context.Photos.Where(m => m.ProductId == productResponse.Id).ToListAsync();

//        // Delete the existing photos from the database and from storage
//        foreach (var item in photos)
//        {
//            image.DeleteImageAsync(item.ImageName);
//        }
//        context.RemoveRange(photos);

//        // Add the new photos to the database and to storage
//        var ImagePath = await image.AddImageAsync(productResponse.Photo, productResponse.Name);
//        var newPhotos = ImagePath.Select(path => new Photo
//        {
//            ImageName = path,
//            ProductId = productResponse.Id
//        }).ToList();
//        await context.AddRangeAsync(newPhotos);
//    }

//    // Save changes to the database
//    await context.SaveChangesAsync();
//    return true;
//}
