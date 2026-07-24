using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public class ProductMasterViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepository;

    public ProductMasterViewModel(IProductRepository productRepository)
    {
        _productRepository = productRepository;

        Products = new ObservableCollection<Product>();
        NewCommand = new RelayCommand(StartNew);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => EditingProduct is not null && !string.IsNullOrWhiteSpace(EditingProduct.ProductName));
        DeleteCommand = new AsyncRelayCommand<Product>(DeleteAsync);
        EditCommand = new RelayCommand<Product>(Edit);

        _ = LoadAsync();
    }

    public ObservableCollection<Product> Products { get; }

    private Product? _editingProduct;
    public Product? EditingProduct
    {
        get => _editingProduct;
        set { SetProperty(ref _editingProduct, value); ((AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged(); }
    }

    private bool _isEditing;
    public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public ICommand NewCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand<Product> DeleteCommand { get; }
    public ICommand EditCommand { get; }

    private async Task LoadAsync()
    {
        var products = await _productRepository.GetAllAsync(activeOnly: false);
        Products.Clear();
        foreach (var p in products) Products.Add(p);
    }

    private void StartNew()
    {
        EditingProduct = new Product { IsActive = true, CodePrefix = "KIT" };
        IsEditing = true;
    }

    private void Edit(Product? product)
    {
        if (product is null) return;
        // Edit a shallow copy so cancelling doesn't leave the grid in a half-changed state.
        EditingProduct = new Product
        {
            Id = product.Id,
            ProductName = product.ProductName,
            Quantity = product.Quantity,
            MinWeightKg = product.MinWeightKg,
            MaxWeightKg = product.MaxWeightKg,
            CodePrefix = product.CodePrefix,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt
        };
        IsEditing = true;
    }

    private async Task SaveAsync()
    {
        if (EditingProduct is null) return;

        if (EditingProduct.MinWeightKg <= 0 || EditingProduct.MaxWeightKg <= 0 || EditingProduct.MinWeightKg > EditingProduct.MaxWeightKg)
        {
            StatusMessage = "Minimum weight must be greater than 0 and less than or equal to maximum weight.";
            return;
        }

        if (EditingProduct.Id == 0)
            await _productRepository.AddAsync(EditingProduct);
        else
            await _productRepository.UpdateAsync(EditingProduct);

        StatusMessage = $"'{EditingProduct.ProductName}' saved.";
        IsEditing = false;
        EditingProduct = null;
        await LoadAsync();
    }

    private async Task DeleteAsync(Product? product)
    {
        if (product is null) return;
        await _productRepository.DeleteAsync(product.Id);
        StatusMessage = $"'{product.ProductName}' deactivated.";
        await LoadAsync();
    }
}
