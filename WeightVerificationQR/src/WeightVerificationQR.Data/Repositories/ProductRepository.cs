using Microsoft.EntityFrameworkCore;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Data.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context) => _context = context;

    public async Task<List<Product>> GetAllAsync(bool activeOnly = true)
    {
        var query = _context.Products.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.ProductName).ToListAsync();
    }

    public Task<Product?> GetByIdAsync(int id) =>
        _context.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Product> AddAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        product.UpdatedAt = DateTime.Now;
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        // Soft delete only - historical WeighRecords reference this product by name/snapshot,
        // so we never hard-delete a product that may have production history.
        var product = await _context.Products.FindAsync(id);
        if (product is null) return;
        product.IsActive = false;
        product.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
    }
}
