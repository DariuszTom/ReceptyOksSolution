using System;
using System.Collections.Generic;
using System.Text;

namespace ReceptyOks.Views
{
    public partial class IngriednientsCalculationViewModel : ObservableObject
    {
        ILocalDatabase _localDatabase;
        [ObservableProperty]
        public ObservableCollection<RecipeLocal> recipes = [];
        [ObservableProperty]
        private string searchQuery = string.Empty;
        public IngriednientsCalculationViewModel(ILocalDatabase localDatabase)
        {
            _localDatabase = localDatabase;
        }
        [RelayCommand]
        public async Task LoadRecipiesAsync()
        {
           var recipesList = await _localDatabase.GetRecipesAsync().ConfigureAwait(false);
            var recipeList = string.IsNullOrWhiteSpace(SearchQuery)
                        ? await _localDatabase.GetRecipesAsync()
                        : await _localDatabase.SearchRecipesAsync(SearchQuery);

            Recipes = new ObservableCollection<RecipeLocal>(recipeList);
        }
    }
}
