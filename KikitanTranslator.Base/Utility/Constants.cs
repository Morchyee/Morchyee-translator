namespace KikitanTranslator.Utility;

public class Constants
{
    public static readonly string BING_TRUSTED_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    public static readonly string BING_MS_VERSION = "1-145.0.3800.70";

    public static readonly string GROQ_PROMPT = "Translate from LANG_SRC to LANG_TARGET faithfully and naturally. Preserve meaning and tone. Output only the translation, without commentary or Markdown.";
    public static readonly string GROQ_MODEL = "qwen/qwen3.8-27b";
}
