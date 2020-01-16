using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using ReceiptScanner.Models;

namespace ReceiptScanner
{
  public class Word
  {
    public List<int> boundingBox { get; set; }
    public string text { get; set; }
    public string confidence { get; set; }
  }
  public class Line
  {
    public List<int> boundingBox { get; set; }
    public string text { get; set; }
    public List<Word> words { get; set; }
  }
  public class recognitionResult 
  {
    public string page { get; set; }
    public double clockwiseOrientation { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public string unit { get; set; }
    public List<Line> lines { get; set; }

  }
  public class ResRoot
  {
    public string status;
    public List<recognitionResult> recognitionResults;
  }
  public class Item
  {
    public string Name { get; set; }
    public double price { get; set; }
  }
  public class Recognizer
  {
    public List<ItemToView> recognizeIt(string FileToRecognize)
    {   

      // string subscriptionKey = Environment.GetEnvironmentVariable("COMPUTER_VISION_SUBSCRIPTION_KEY");
      // string endpoint = Environment.GetEnvironmentVariable("COMPUTER_VISION_ENDPOINT");
      string subscriptionKey = "b3e46781e1e648c9bc8452b124b73c31";
      string endpoint = "https://westcentralus.api.cognitive.microsoft.com/vision";
      string uriBase = endpoint + "/v2.1/read/core/asyncBatchAnalyze";
      
      string imageFile = FileToRecognize;

      ReadText(imageFile).Wait();
      
      async Task ReadText(string imageFilePath)
      {
        try
        {
          HttpClient client = new HttpClient();

          // Request headers.
          client.DefaultRequestHeaders.Add(
          "Ocp-Apim-Subscription-Key", subscriptionKey);

        // Assemble the URI for the REST API method.
        string uri = uriBase;

        HttpResponseMessage response;

        
        string operationLocation;

        // Reads the contents of the specified local image
        // into a byte array.
        byte[] byteData = GetImageAsByteArray(imageFilePath);

        // Adds the byte array as an octet stream to the request body.
        using (ByteArrayContent content = new ByteArrayContent(byteData))
        {
          // This example uses the "application/octet-stream" content type.
          // The other content types you can use are "application/json"
          // and "multipart/form-data".
          content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

          // The first REST API method, Batch Read, starts
          // the async process to analyze the written text in the image.
          response = await client.PostAsync(uri, content);
        }

        // The response header for the Batch Read method contains the URI
        // of the second method, Read Operation Result, which
        // returns the results of the process in the response body.
        // The Batch Read operation does not return anything in the response body.
        if (response.IsSuccessStatusCode)
          operationLocation =
            response.Headers.GetValues("Operation-Location").FirstOrDefault();
        else
        {
          // Display the JSON error data.
          string errorString = await response.Content.ReadAsStringAsync();
          Console.WriteLine("\n\nResponse:\n{0}\n",
            JToken.Parse(errorString).ToString());
          return;
        }

        // If the first REST API method completes successfully, the second 
        // REST API method retrieves the text written in the image.
        //
        // Note: The response may not be immediately available. Text
        // recognition is an asynchronous operation that can take a variable
        // amount of time depending on the length of the text.
        // You may need to wait or retry this operation.
        //
        // This example checks once per second for ten seconds.
        string contentString;
        int i = 0;
        do
        {
          System.Threading.Thread.Sleep(1000);
          response = await client.GetAsync(operationLocation);
          contentString = await response.Content.ReadAsStringAsync();
          ++i;
        }
        while (i < 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1);

        if (i == 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1)
        {
          Console.WriteLine("\nTimeout error.\n");
          return;
        }

        
        //JToken.Parse(contentString).ToString());
        File.WriteAllText("output.json", JToken.Parse(contentString).ToString());
        
      }
      catch (Exception e) 
      { 
       
        Console.WriteLine("\n" + e.Message);
      }
    }

    
    byte[] GetImageAsByteArray(string imageFilePath)
    {
      // Open a read-only file stream for the specified file.
      using (FileStream fileStream =
        new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
      {
        // Read the file's contents into a byte array.
        BinaryReader binaryReader = new BinaryReader(fileStream);
        return binaryReader.ReadBytes((int)fileStream.Length);
      }
    }
  

    List<Word> CreateListOfWords(ResRoot rslt)
    {
      List<Word> All_Words = new List<Word>();
      for (int i = 0; i < rslt.recognitionResults.Count; i++)
      {
        foreach (Line l in rslt.recognitionResults[i].lines)
        {
          foreach (Word w in l.words)
          {
            All_Words.Add(w);
          }
        }
      }
      return All_Words;
    }

    List<List<Word>> WordsInLines(List<Word> _AllWords)
    {
      _AllWords = _AllWords.OrderBy(w => Tuple.Create(w.boundingBox[1], w.boundingBox[0])).ToList();
      List<List<Word>> WordsInLine = new List<List<Word>>();
      List<Word> Line = new List<Word>();
      int lineHeight = _AllWords[0].boundingBox[5] - _AllWords[0].boundingBox[3];
      int prev = _AllWords[0].boundingBox[3];
      
      for(int i = 0; i < _AllWords.Count;i++)
      {
        if ((Math.Abs(_AllWords[i].boundingBox[3] - prev) < lineHeight))
        {
          Line.Add(_AllWords[i]);
          
        }
        else
        {
          WordsInLine.Add(Line.OrderBy(l => l.boundingBox[0]).ToList());
          Line = new List<Word>();
          
          Line.Add(_AllWords[i]);
          lineHeight = _AllWords[i].boundingBox[5] - _AllWords[i].boundingBox[3];
          prev = _AllWords[i].boundingBox[3];
        }
      }

      if (Line.Count > 0)
        WordsInLine.Add(Line.OrderBy(l => l.boundingBox[0]).ToList());

      
      return WordsInLine;
    }

    List<List<string>> WordsToStrings(List<List<Word>> WordsInLine)
    {
      List<List<string>> _Lines = new List<List<string>>();

      
      foreach(var l in WordsInLine)
      {
        List<string> Line = new List<string>();

        foreach(var w in l)
        {

          Line.Add(w.text);

        }
        _Lines.Add(Line);
      }
      return _Lines;
    }

    ItemToView ParseLine(List<string> Line)
    {
      ItemToView item = new ItemToView();
      item.ItemName = "";
      item.ItemPrice = 0;
      if (Line.Count > 0)

      {   
        string path = "./results/result.txt";
        if (!File.Exists(path)) 
        {
          // Create a file to write to.
          using (StreamWriter outputFile = File.CreateText(path)) 
          {
            outputFile.WriteLine("Results: ");
          }	
        }
        using (StreamWriter outputFile = File.AppendText(path))
        {
          outputFile.WriteLine("****************************");
          foreach (string s in Line)
          {
            if (s.Length > 2 && Char.IsLetter(s, 0))
            {
              string ItemName = s;
              outputFile.WriteLine($"Name probably is {ItemName}");
              item.ItemName = item.ItemName + " " + s;
            }

            else if (Char.IsDigit(s, 0) && s.Contains("."))
            {
              try
              {
                double ItemPrice = Double.Parse(s);
                outputFile.WriteLine($"Price probably is {ItemPrice}");
                item.ItemPrice = Double.Parse(s);
              }
              catch (FormatException)
              {
                outputFile.WriteLine($"Unable to parse {s}'");
              }
            }
            else if (Char.IsDigit(s, 0) && s.Contains("/") && s.Count() > 5)
            {
              DateTime date;
              if (DateTime.TryParse(s, out date))
                outputFile.WriteLine($"Date probably is {date}");
              else
                outputFile.WriteLine($"Unable to parse {s}'");
            }

            else outputFile.WriteLine($"Unable to parse '{s}");
          }
        }
      }
      return item;
    }
    ResRoot res = JsonConvert.DeserializeObject<ResRoot>(File.ReadAllText("output.json"));
    List<Word> AllWords = CreateListOfWords(res);
        List<List<Word>> AllWordsInLines = WordsInLines(AllWords);
    List<ItemToView> resultList = new List<ItemToView>();
        List<List<string>> Lines = WordsToStrings(AllWordsInLines);
        
        foreach (var l in Lines)
        {
          resultList.Add(ParseLine(l));
        }
    return resultList;
    }
  }
}