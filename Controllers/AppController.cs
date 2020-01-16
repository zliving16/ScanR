using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using ReceiptScanner;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;

namespace ReceiptScanner.Controllers
{
  public class HomeController : Controller
  {
    private IHostingEnvironment _env;
    private string _imgUploadDir;
    private List<byte[]> _imgs_bytes;
    private List<string> _imgs_base64;

    private MyContext db;

    public HomeController(IHostingEnvironment env,
                          MyContext context)
    {
      _env = env;
      _imgUploadDir = "./wwwroot/uploads";
      _imgs_bytes = new List<byte[]>();
      _imgs_base64 = new List<string>();
      db = context;
    }
    [HttpGet("")]
    public IActionResult LandingPage(){
      return View("landingPage");
    }
  
    [Authorize]
    [HttpGet("dashboard")]
    public IActionResult Index()
    {
      return View();
    }

    [Authorize]
    [HttpPost("upload")]
    public async Task<IActionResult> Process(IEnumerable<IFormFile> files)
    {
      long size = files.Sum(f => f.Length);

      List<string> filePaths = new List<string>();
      List<byte[]> imgs_bytes = new List<byte[]>();
      foreach (var file in files)
      {
        if (file.Length > 0 && file.ContentType.Contains("image"))
        {
          string datetime = DateTime.Now.ToString("yyyy_MM_dd__HH_mm_ss");
          string filetype = "." + file.ContentType.Split("/")[1];
          string filename = datetime + filetype;
          string filePath = $"{_imgUploadDir}/{filename}";
          filePaths.Add(filePath);
          
          string type = file.GetType().ToString();
          
          using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
          {
            await file.CopyToAsync(stream);
          }
        }
      }

      // process uploaded files
      CryptoEngine Encryptor = new CryptoEngine();
      foreach (string path in filePaths){
        string encryptedPath = Encryptor.Encrypt(path);
        Photos img = new Photos {
          PhotoPath = encryptedPath
        };
        db.Add(img);
      }
      db.SaveChanges();
      // Don't rely on or trust the FileName property without validation.


      string[] firstfilepath = filePaths[0].Split("\\");
      int lastsegment = firstfilepath.Length-1;
      string firstfilename = firstfilepath[lastsegment];
      Recognizer r = new Recognizer();
      List<ItemToView> ResultList = r.recognizeIt($"./wwwroot/uploads/{firstfilename}");
      return View("Result", ResultList);
    
    }

      [Authorize]
    [HttpGet("result")]
    public IActionResult Result(List<ItemToView> resultList)
    {
      return View(resultList);
    }

    public IActionResult UploadedImages(List<byte[]> imgs_bytes)
    {

      List<string> base64imgs = new List<string>();
      imgs_bytes = (List<byte[]>)TempData["imgs"];
      foreach (byte[] imgbytes in imgs_bytes)
      {
        base64imgs.Add(Convert.ToBase64String(imgbytes));
      }
      return View(base64imgs);
    }

    public IActionResult Privacy()
    {
      return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
      return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
  }
}
