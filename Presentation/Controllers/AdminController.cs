using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;
using Newtonsoft.Json;

namespace Presentation.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Create() //will load the page where the user can insert a new menu
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(MenuItem m) // will handle the additio of a new menuitem
        {//code which will save a new menutiem in the cache

            try
            {
                ConnectionMultiplexer cm = ConnectionMultiplexer.Connect("");
                var db = cm.GetDatabase();

                //1. get all the existent menuitems
                var myMenuItems = db.StringGet("menuitems");

                List<MenuItem> myList = new List<MenuItem>();
                if (myMenuItems.IsNullOrEmpty)
                {
                    myList = new List<MenuItem>();
                }
                else
                {
                    //json string >>>>> obj (List<MenuItem>)
                    myList = JsonConvert.DeserializeObject<List<MenuItem>>(myMenuItems);
                }

                //2. add the new menu item to existent list of menu items
                myList.Add(m);

                //3. save everything back into the cache
                //obj (List<MenuItem>) >> string
                var myJsonString = JsonConvert.SerializeObject(myList);
                db.StringSet("menuitems", myJsonString);

                ViewBag.Message = "Saved in cache"; //a way how you can pass on a user friendly message
            }catch(Exception ex)
            {
                ViewBag.Error = "Not saved in cache";
            }
            return View();
        }
    }
}
