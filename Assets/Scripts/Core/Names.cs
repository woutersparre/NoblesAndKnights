using UnityEngine;
using System.Collections.Generic;
class Names
{
    public static readonly List<string> randomLocationNames = new List<string>
    {
    "Alderwyn", "Brackenmoor", "Caldermere", "Draychester", "Eldermere",
    "Frosthaven", "Glenwarren", "Harrowgate", "Ironwylde", "Kingsmere",
    "Larkhaven", "Marlewyn", "Northwynd", "Oakenthal", "Prythvale",
    "Quenndale", "Ravenwynne", "Southmarch", "Tarnwick", "Valthorne",
    "Westermoor", "Yarndale", "Ashwynde", "Brimwatch", "Crestwylde",
    "Dornfall", "Elmbrook", "Fallowmere", "Greystone", "Havenreach",
    "Isenford", "Kelthorne", "Lyndwyn", "Mornhold", "Netherton",
    "Oakenwyld", "Pendlehurst", "Ridgewylde", "Stormholt", "Thornvale",
    "Umberlyn", "Vexholme", "Wolfsrest", "Wyvernshade", "Zephyrlake",
    "Amberlyn", "Briarwynd", "Coldmarsh", "Dunvarren", "Eastmere",
    "Fairhaven", "Grayfen", "Halewyld", "Invermere", "Kestleford",
    "Lochwynne", "Millshade", "Norhaven", "Oakhollow", "Pinecrest",
    "Quellborough", "Ravenford", "Stonewylde", "Thistlemere", "Valehaven",
    "Windmere", "Ashenholt", "Blackmere", "Caerwynd", "Duskendale",
    "Evershade", "Flintmere", "Havenmoor", "Ironfell", "Kingsward",
    "Lanedale", "Mosswylde", "Northhelm", "Orrenholme", "Perdane",
    "Ravenmarsh", "Sablewynd", "Stormwylde", "Thornwick", "Ulthwyne",
    "Vermere", "Whitestone", "Wolfwynne", "Wyvernholt", "Zentharne",
    "Avermere", "Brambleholt", "Cragwynd", "Driftmoor", "Eldenward",
    "Fernwylde", "Glimmerholt", "Harrowsreach", "Ivorymere", "Kyneswood",
    "Lunefell", "Mistwylde", "Norcroft", "Orrenwald", "Pierstow",
    "Quorndale", "Shadowwylde", "Stormhaven", "Thornebriar", "Vordalyn",
    "Westwylde", "Wolvenmere", "Zephyra"
    };

    private static readonly List<string> MaleFirstNames = new List<string>
    {
    "Alaric", "Aldous", "Archibald", "Arne", "Baldric", "Bartholomew", "Bertram",
    "Cedric", "Clovis", "Cuthbert", "Eldric", "Ernest", "Fulk", "Godfrey", "Harold",
    "Leofric", "Luther", "Osric", "Percival", "Ragnar"
    };

    // Female first names
    private static readonly List<string> FemaleFirstNames = new List<string>
    {
    "Adelaide", "Beatrice", "Cecily", "Edith", "Eleanor", "Emma", "Giselle",
    "Isolde", "Matilda", "Margery", "Philippa", "Rosalind", "Sybilla", "Winifred",
    "Yvette", "Helena", "Aveline", "Clarice", "Isabella", "Blanche"
    };

    // Neutral/Genderless first names (optional, if needed)
    private static readonly List<string> NeutralFirstNames = new List<string>
    {
    "Robin", "Jordan", "Taylor", "Alex", "Morgan", "Casey", "Avery", "Quinn",
    "Riley", "Jamie"
    };

    // Last names
    private static readonly List<string> LastNames = new List<string>
    {
    "Smith", "Taylor", "Cooper", "Walker", "Wright", "Clark", "Turner", "Carter",
    "Parker", "Cook", "Baker", "Fletcher", "Mason", "Miller", "Potter", "Sawyer",
    "Weaver", "Wood", "Chandler", "Chapman"
    };
    public static string GenerateRandomName(Gender gender = Gender.Male, string lastName = null)
    {
        System.Random random = new();

        // Select first name based on Gender
        string firstName = gender switch
        {
            Gender.Male => MaleFirstNames[random.Next(MaleFirstNames.Count)],
            Gender.Female => FemaleFirstNames[random.Next(FemaleFirstNames.Count)],
            _ => NeutralFirstNames[random.Next(NeutralFirstNames.Count)], // Default for Genderless
        };

        // Randomly pick a last name
        lastName ??= LastNames[random.Next(LastNames.Count)];
        return $"{firstName} {lastName}";
    }
    public static string GetLastName(string fullName)
    {
        // Split the full name by space and return the last part as the last name
        string[] nameParts = fullName.Split(' ');

        if (nameParts.Length > 1)
        {
            return nameParts[nameParts.Length - 1];  // Last element is the last name
        }
        else
        {
            return string.Empty;  // No last name found, return empty string or handle as needed
        }
    }
}