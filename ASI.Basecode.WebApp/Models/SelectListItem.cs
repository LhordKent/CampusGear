namespace ASI.Basecode.WebApp.Models
{
    /// <summary>One choice in a filter drop-down: the submitted value and the label shown to the user.</summary>
    public class SelectListItem
    {
        public SelectListItem(string text = "", string value = "")
        {
            this.Label = text;
            this.Value = value;
        }

        public string Label { get; set; }

        public string Value { get; set; }
    }
}
