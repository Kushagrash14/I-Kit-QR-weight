using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public class ProductMasterViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductMasterViewModel> _logger;

    public ProductMasterViewModel(
        IProductRepository productRepository,
        ILogger<ProductMasterViewModel> logger)
    {
        _productRepository = productRepository;
        _logger = logger;

        Products = new ObservableCollection<Product>();
        NewCommand = new RelayCommand(StartNew);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => EditingProduct is not null);
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
        try
        {
            var products = await _productRepository.GetAllAsync(activeOnly: false);
            Products.Clear();
            foreach (var p in products) Products.Add(p);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product list could not be loaded.");
            StatusMessage = $"Products could not be loaded: {GetSpecificError(ex)}";
        }
    }

    private void StartNew()
    {
        EditingProduct = new Product
        {
            IsActive = true,
            CodePrefix = "KIT",
            CommandCode = "P"
        };
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
            CommandCode = product.CommandCode,
            LabelLineCode = product.LabelLineCode,
            ModelCode = product.ModelCode,
            LabelSizeText = product.LabelSizeText,
            LabelLengthText = product.LabelLengthText,
            LabelMaterialText = product.LabelMaterialText,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt
        };
        IsEditing = true;
    }

    private async Task SaveAsync()
    {
        if (EditingProduct is null) return;

        EditingProduct.ProductName = EditingProduct.ProductName?.Trim() ?? string.Empty;
        EditingProduct.Quantity = EditingProduct.Quantity?.Trim() ?? string.Empty;
        EditingProduct.CodePrefix = NormalizeCode(EditingProduct.CodePrefix);
        EditingProduct.CommandCode = NormalizeCode(EditingProduct.CommandCode);
        EditingProduct.LabelLineCode = NormalizeCode(EditingProduct.LabelLineCode);
        EditingProduct.ModelCode = NormalizeModelCode(EditingProduct.ModelCode);
        EditingProduct.LabelSizeText = EditingProduct.LabelSizeText?.Trim() ?? string.Empty;
        EditingProduct.LabelLengthText = EditingProduct.LabelLengthText?.Trim() ?? string.Empty;
        EditingProduct.LabelMaterialText = EditingProduct.LabelMaterialText?.Trim() ?? string.Empty;

        if (EditingProduct.ProductName.Length == 0)
        {
            StatusMessage = "Product name is required.";
            return;
        }

        if (EditingProduct.ProductName.Length > 200 || EditingProduct.Quantity.Length > 50)
        {
            StatusMessage = "Product name must be 200 characters or fewer and quantity must be 50 characters or fewer.";
            return;
        }

        if (EditingProduct.MinWeightKg <= 0 || EditingProduct.MaxWeightKg <= 0 || EditingProduct.MinWeightKg > EditingProduct.MaxWeightKg)
        {
            StatusMessage = "Minimum weight must be greater than 0 and less than or equal to maximum weight.";
            return;
        }

        if (EditingProduct.CommandCode.Length == 0 ||
            EditingProduct.LabelLineCode.Length == 0 ||
            EditingProduct.ModelCode.Length == 0)
        {
            StatusMessage = "Command, line and model codes must contain valid letters or numbers.";
            return;
        }

        if (EditingProduct.CodePrefix.Length == 0)
            EditingProduct.CodePrefix = "KIT";

        if (EditingProduct.CodePrefix.Length > 10 ||
            EditingProduct.CommandCode.Length > 10 ||
            EditingProduct.LabelLineCode.Length > 20 ||
            EditingProduct.ModelCode.Length > 50)
        {
            StatusMessage = "Prefix/command must be 10 characters or fewer, line 20 or fewer, and model code 50 or fewer.";
            return;
        }

        if (EditingProduct.LabelSizeText.Length == 0 ||
            EditingProduct.LabelLengthText.Length == 0 ||
            EditingProduct.LabelMaterialText.Length == 0)
        {
            StatusMessage = "Label size, length and material text are required for printing.";
            return;
        }

        if (EditingProduct.LabelSizeText.Length > 100 ||
            EditingProduct.LabelLengthText.Length > 50 ||
            EditingProduct.LabelMaterialText.Length > 50)
        {
            StatusMessage = "Label size must be 100 characters or fewer; length and material must be 50 or fewer.";
            return;
        }

        try
        {
            if (EditingProduct.Id == 0)
                await _productRepository.AddAsync(EditingProduct);
            else
                await _productRepository.UpdateAsync(EditingProduct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Product {ProductName} could not be saved.",
                EditingProduct.ProductName);
            StatusMessage = $"Product could not be saved: {GetSpecificError(ex)}";
            return;
        }

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

    private static string NormalizeCode(string value) =>
        new((value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static string NormalizeModelCode(string value) =>
        new((value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
            .ToArray());

    private static string GetSpecificError(Exception exception)
    {
        var specific = exception.GetBaseException().Message;
        return string.IsNullOrWhiteSpace(specific) ? exception.Message : specific;
    }
}
