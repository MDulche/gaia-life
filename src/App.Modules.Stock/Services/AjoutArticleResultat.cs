namespace App.Modules.Stock.Services;

/// <summary>Résultat de création d'article (création nette ou proposition / exécution de fusion).</summary>
public sealed class AjoutArticleResultat
{
    public required bool Cree { get; init; }

    public required bool FusionProposee { get; init; }

    public required bool FusionEffectuee { get; init; }

    public required int ArticleId { get; init; }

    public string? Message { get; init; }

    public static AjoutArticleResultat CreeNouveau(int articleId) => new()
    {
        Cree = true,
        FusionProposee = false,
        FusionEffectuee = false,
        ArticleId = articleId
    };

    public static AjoutArticleResultat ProposerFusion(int articleId, string nom) => new()
    {
        Cree = false,
        FusionProposee = true,
        FusionEffectuee = false,
        ArticleId = articleId,
        Message = $"Un article nommé « {nom} » existe déjà. Fusionner la quantité avec l'existant ?"
    };

    public static AjoutArticleResultat Fusionne(int articleId) => new()
    {
        Cree = false,
        FusionProposee = false,
        FusionEffectuee = true,
        ArticleId = articleId,
        Message = "Quantité ajoutée à l'article existant."
    };
}
