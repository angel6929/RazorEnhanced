namespace RazorEnhanced.Macros.Actions
{
    public class HelpButtonAction : MacroAction
    {
        public override string GetActionName() => "HelpButton";

        public override void Execute()
        {
            Player.HelpButton();
        }

        public override string Serialize()
        {
            return "HelpButton";
        }

        public override void Deserialize(string data)
        {
        }
    }
}
