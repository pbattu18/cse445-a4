using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConsoleApp1
{
    public class Program
    {
        // LIVE GitHub Pages URLs
        public static string xmlURL = "https://raw.githubusercontent.com/pbattu18/cse445-a4/master/Hotels.xml";
        public static string xmlErrorURL = "https://raw.githubusercontent.com/pbattu18/cse445-a4/master/HotelsErrors.xml";
        public static string xsdURL = "https://raw.githubusercontent.com/pbattu18/cse445-a4/master/Hotels.xsd";

        public static void Main(string[] args)
        {
            // 1) Validate the good XML (should print exactly: No errors are found)
            string result = Verification(xmlURL, xsdURL);
            Console.WriteLine(result);

            // 2) Validate the broken XML (should print collected errors/exceptions)
            result = Verification(xmlErrorURL, xsdURL);
            Console.WriteLine(result);

            // 3) Convert valid XML to JSON (omit _Rating when missing)
            result = Xml2Json(xmlURL);
            Console.WriteLine(result);
        }

        // Validate XML (by URL) against XSD (by URL).
        // Return "No errors are found" when valid; otherwise return joined error messages (or exception text).
        public static string Verification(string xmlUrl, string xsdUrl)
        {
            var messages = new List<string>();

            try
            {
                var settings = new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema,
                    DtdProcessing = DtdProcessing.Prohibit
                };

                settings.Schemas.Add(null, xsdUrl);
                settings.ValidationFlags =
                    XmlSchemaValidationFlags.ReportValidationWarnings |
                    XmlSchemaValidationFlags.ProcessInlineSchema |
                    XmlSchemaValidationFlags.ProcessSchemaLocation;

                settings.ValidationEventHandler += (sender, e) =>
                {
                    string where = "";
                    if (sender is IXmlLineInfo li && li.HasLineInfo())
                        where = $"(line {li.LineNumber}, pos {li.LinePosition}) ";
                    messages.Add($"{e.Severity}: {where}{e.Message}");
                };

                using (var reader = XmlReader.Create(xmlUrl, settings))
                {
                    while (reader.Read()) { /* validation happens during Read() */ }
                }
            }
            catch (Exception ex)
            {
                messages.Add($"Exception: {ex.Message}");
            }

            return messages.Count == 0 ? "No errors are found" : string.Join("\n", messages);
        }

        // Convert valid XML (by URL) to JSON with required shape:
        // - Multiple <Phone> -> array
        // - Address as object with Number/Street/City/State/Zip/NearestAirport
        // - Optional Rating attribute on <Hotel> becomes "_Rating" (omit if absent)
        public static string Xml2Json(string xmlUrl)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.Load(xmlUrl);

            var hotelsArray = new JArray();
            var hotelNodes = doc.SelectNodes("/Hotels/Hotel");
            if (hotelNodes != null)
            {
                foreach (XmlNode hotel in hotelNodes)
                {
                    var jHotel = new JObject();

                    // Name
                    var nameNode = hotel.SelectSingleNode("Name");
                    if (nameNode != null)
                        jHotel["Name"] = nameNode.InnerText.Trim();

                    // Phones -> array
                    var phoneNodes = hotel.SelectNodes("Phone");
                    if (phoneNodes != null && phoneNodes.Count > 0)
                    {
                        var phones = new JArray();
                        foreach (XmlNode p in phoneNodes)
                        {
                            var t = p.InnerText.Trim();
                            if (!string.IsNullOrEmpty(t)) phones.Add(t);
                        }
                        if (phones.Count > 0)
                            jHotel["Phone"] = phones;
                    }

                    // Address object
                    var addr = hotel.SelectSingleNode("Address");
                    if (addr != null)
                    {
                        var jAddr = new JObject();

                        void put(string tag)
                        {
                            var n = addr.SelectSingleNode(tag);
                            if (n != null && !string.IsNullOrWhiteSpace(n.InnerText))
                                jAddr[tag] = n.InnerText.Trim();
                        }

                        put("Number");
                        put("Street");
                        put("City");
                        put("State");
                        put("Zip");
                        put("NearestAirport");

                        if (jAddr.Count > 0)
                            jHotel["Address"] = jAddr;
                    }

                    // Optional Rating attribute -> "_Rating"
                    var rating = hotel.Attributes?["Rating"]?.Value;
                    if (!string.IsNullOrWhiteSpace(rating))
                        jHotel["_Rating"] = rating.Trim();

                    hotelsArray.Add(jHotel);
                }
            }

            var root = new JObject
            {
                ["Hotels"] = new JObject
                {
                    ["Hotel"] = hotelsArray
                }
            };

            return root.ToString(Newsoft.Json.Formatting.None);
        }
    }
}





