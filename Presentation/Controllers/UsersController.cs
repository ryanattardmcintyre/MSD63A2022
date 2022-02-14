using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserModel = Presentation.Models.User;

namespace Presentation.Controllers
{
    public class UsersController : Controller
    {
        string project = "";
        public UsersController(IConfiguration configuration)
        {
            project = configuration["project"];
        }

        //where user can see his/her details
        [Authorize]
        public async Task<IActionResult> Index() //get
        {

            //1. we have to get the user with the logged in email 
            //2. if user exists
            //3. show it on the page
            //4. if user does not exist we show him blank details

            FirestoreDb db = FirestoreDb.Create(project);

            DocumentReference docRef = db.Collection("users").Document(User.Claims.ElementAt(4).Value);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            UserModel myUser = new UserModel();
            myUser.Email = User.Claims.ElementAt(4).Value;

            if (snapshot.Exists)
            {
                Console.WriteLine("Document data for {0} document:", snapshot.Id);
                Dictionary<string, object> userFromDb = snapshot.ToDictionary();

                myUser.FirstName = userFromDb["firstName"].ToString();
                myUser.LastName = userFromDb["lastName"].ToString();
            }
            
            return View(myUser);
        }

        //adds user details
        public IActionResult Register() //post
        {
            return View();
        }

        //send a message to someone else
        public IActionResult Send()
        {
            return View();
        }

        //List all messages sent from history
        public IActionResult List()
        {
            return View();
        }
    }
}
