using Microsoft.EntityFrameworkCore;

namespace Systematikken.Api.Data;

public static class Seed
{
    /// <summary>The 98 Danish municipalities. Their rules start unconfirmed; the first institution confirms them.</summary>
    public static readonly string[] MunicipalityNames =
    [
        "Albertslund", "Allerød", "Assens", "Ballerup", "Billund", "Bornholm", "Brøndby", "Brønderslev", "Dragør", "Egedal",
        "Esbjerg", "Fanø", "Favrskov", "Faxe", "Fredensborg", "Fredericia", "Frederiksberg", "Frederikshavn", "Frederikssund", "Furesø",
        "Faaborg-Midtfyn", "Gentofte", "Gladsaxe", "Glostrup", "Greve", "Gribskov", "Guldborgsund", "Haderslev", "Halsnæs", "Hedensted",
        "Helsingør", "Herlev", "Herning", "Hillerød", "Hjørring", "Holbæk", "Holstebro", "Horsens", "Hvidovre", "Høje-Taastrup",
        "Hørsholm", "Ikast-Brande", "Ishøj", "Jammerbugt", "Kalundborg", "Kerteminde", "Kolding", "København", "Køge", "Langeland",
        "Lejre", "Lemvig", "Lolland", "Lyngby-Taarbæk", "Læsø", "Mariagerfjord", "Middelfart", "Morsø", "Norddjurs", "Nordfyns",
        "Nyborg", "Næstved", "Odder", "Odense", "Odsherred", "Randers", "Rebild", "Ringkøbing-Skjern", "Ringsted", "Roskilde",
        "Rudersdal", "Rødovre", "Samsø", "Silkeborg", "Skanderborg", "Skive", "Slagelse", "Solrød", "Sorø", "Stevns",
        "Struer", "Svendborg", "Syddjurs", "Sønderborg", "Thisted", "Tønder", "Tårnby", "Vallensbæk", "Varde", "Vejen",
        "Vejle", "Vesthimmerland", "Viborg", "Vordingborg", "Ærø", "Aabenraa", "Aalborg", "Aarhus",
    ];

    public static async Task MunicipalitiesAsync(AppDb db)
    {
        if (await db.Municipalities.AnyAsync()) return;
        db.Municipalities.AddRange(MunicipalityNames.Select(n => new Municipality { Name = n }));
        await db.SaveChangesAsync();
    }
}
