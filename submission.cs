using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace ConsoleApp1
{
    public class Program
    {
        // RAW GitHub URLs (confirmed working)
        public static string xmlURL      = "https://raw.githubusercontent.com/pbattu18/cse445-a4/master/Hotels.xml";
        public static string xmlErrorURL = "https://raw.githubusercontent.com/pbattu18/cse445-a4/master/HotelsErrors.xml";
        public static string xsdURL      = "https://raw.githubusercontent.com/pbattu18/cse445-a4/master/Hotels.xsd";

        public static void Main(string[] args)
        {
            // Some graders run older frameworks; ensure TLS 1.2 for GitHub downloads.
#pragma warning disable SYSLIB0014
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
#pragma warning restore SYSLIB0014

            // 1) Validate the GOOD xml
            string result = Verification(xmlURL, xsdURL);
            Console.WriteLine(result);

            // 2) Validate the ERROR xml (should print errors / exception line)
            result = Verification(xmlErrorURL, xsdURL);
            Console.WriteLine(result);

            // 3) Convert valid XML to JSON (shape per spec)
            result = Xml2Json(xmlURL);
            Console.WriteLine(result);
        }

        // Validate XML (by URL) against XSD (by URL).
        // Return EXACT "No errors are found" if valid; else newline-joined messages (or "Exception: ...").
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

        // Convert valid XML (by URL) to JSON with the required shape:
        // {
        //   "Hotels": {
        //     "Hotel": [
        //       {
        //         "Name":"...",
        //         "Phone":[ "...", "..." ],          // omit if none
        //         "Address":{                        // omit if empty
        //           "Number":"...","Street":"...","City":"...","State":"...","Zip":"...","NearestAirport":"..."
        //         },
        //         "_Rating":"..."                   // only if @Rating exists
        //       }, ...
        //     ]
        //   }
        // }
        public static string Xml2Json(string xmlUrl)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.Load(xmlUrl);

            var hotelNodes = doc.SelectNodes("/Hotels/Hotel");
            var sb = new StringBuilder();
            sb.Append("{\"Hotels\":{\"Hotel\":[");

            if (hotelNodes != null)
            {
                bool firstHotel = true;
                foreach (XmlNode hotel in hotelNodes)
                {
                    if (!firstHotel) sb.Append(",");
                    firstHotel = false;

                    sb.Append("{");

                    // Name (emit empty string if missing to keep shape simple)
                    string name = hotel.SelectSingleNode("Name")?.InnerText?.Trim() ?? "";
                    sb.Append("\"Name\":\"").Append(JsonEscape(name)).Append("\"");

                    // Phones (0..n)
                    var phones = hotel.SelectNodes("Phone");
                    if (phones != null && phones.Count > 0)
                    {
                        bool firstPhone = true;
                        sb.Append(",\"Phone\":[");
                        foreach (XmlNode pn in phones)
                        {
                            var t = pn.InnerText?.Trim();
                            if (string.IsNullOrEmpty(t)) continue;
                            if (!firstPhone) sb.Append(",");
                            firstPhone = false;
                            sb.Append("\"").Append(JsonEscape(t)).Append("\"");
                        }
                        sb.Append("]");
                    }

                    // Address object (only include if at least one child present)
                    var addr = hotel.SelectSingleNode("Address");
                    var addrBuf = new StringBuilder();
                    if (addr != null)
                    {
                        void put(string tag)
                        {
                            var n = addr.SelectSingleNode(tag);
                            if (n != null && !string.IsNullOrWhiteSpace(n.InnerText))
                            {
                                if (addrBuf.Length > 0) addrBuf.Append(",");
                                addrBuf.Append("\"").Append(tag).Append("\":\"")
                                       .Append(JsonEscape(n.InnerText.Trim())).Append("\"");
                            }
                        }
                        put("Number"); put("Street"); put("City"); put("State"); put("Zip"); put("NearestAirport");
                    }
                    if (addrBuf.Length > 0)
                    {
                        sb.Append(",\"Address\":{").Append(addrBuf.ToString()).Append("}");
                    }

                    // Optional Rating attribute -> "_Rating"
                    var rating = hotel.Attributes?["Rating"]?.Value?.Trim();
                    if (!string.IsNullOrEmpty(rating))
                    {
                        sb.Append(",\"_Rating\":\"").Append(JsonEscape(rating)).Append("\"");
                    }

                    sb.Append("}");
                }
            }

            sb.Append("]}}");
            return sb.ToString();
        }

        // Minimal JSON string escaper
        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 32)
                            sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else
                            sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}


