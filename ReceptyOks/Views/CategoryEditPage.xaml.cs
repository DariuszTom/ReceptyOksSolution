using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class CategoryEditPage : ContentPage
{
    public CategoryEditPage(CategoryEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
