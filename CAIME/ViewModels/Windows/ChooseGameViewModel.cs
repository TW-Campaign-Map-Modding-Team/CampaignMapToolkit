namespace CAIME.ViewModels
{
    /// <summary>Asks which of several games a file comes from.</summary>
    public class ChooseGameViewModel : BaseViewModel
    {
        public string           Message         { get; }
        public GameTemplate[]   Games           { get; }
        public GameTemplate     SelectedGame    { get; set; }

        public ChooseGameViewModel(string message, GameTemplate[] games)
        {
            Message         = message;
            Games           = games;
            SelectedGame    = games[0];
        }
    }
}
