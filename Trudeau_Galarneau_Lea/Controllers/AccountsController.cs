using Labo3A.Models;
using MDB.Models;
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Web.Mvc;

namespace MDB.Controllers
{
    public class AccountsController : Controller
    {
        private readonly AppDBEntities DB = new AppDBEntities();

        [HttpPost]
        public JsonResult EmailExist(string email)
        {
            return Json(DB.EmailExist(email));
        }
        [HttpPost]
        public JsonResult EmailAvailable(string email, int id = 0)
        {
            return Json(DB.EmailAvailable(email, id));
        }

        #region Login and Logout
        public ActionResult Login(string message)
        {
            ViewBag.Message = message;
            return View(new LoginCredential());
        }
        [HttpPost]
        [ValidateAntiForgeryToken()]
        public ActionResult Login(LoginCredential loginCredential)
        {
            if (ModelState.IsValid)
            {
                if (DB.EmailBlocked(loginCredential.Email))
                {
                    ModelState.AddModelError("Email", "Ce compte est bloqué.");
                    return View(loginCredential);
                }
                if (!DB.EmailVerified(loginCredential.Email))
                {
                    ModelState.AddModelError("Email", "Ce courriel n'est pas vérifié.");
                    return View(loginCredential);
                }
                User user = DB.GetUser(loginCredential);
                if (user == null)
                {
                    ModelState.AddModelError("Password", "Mot de passe incorrecte.");
                    return View(loginCredential);
                }
                if (OnlineUsers.IsOnLine(user.Id))
                {
                    ModelState.AddModelError("Email", "Cet usager est déjà connecté.");
                    return View(loginCredential);
                }
                OnlineUsers.AddSessionUser(user.Id);
                Session["currentLoginId"] = DB.AddLogin(user.Id).Id;
                return RedirectToAction("Index", "Movies");
            }
            return View(loginCredential);
        }
        public ActionResult Logout()
        {
            if (Session["currentLoginId"] != null)
                DB.UpdateLogout((int)Session["currentLoginId"]);
            OnlineUsers.RemoveSessionUser();
            return RedirectToAction("Login");
        }
        #endregion

        #region Create and Edit
        public ActionResult Subscribe()
        {
            ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
            return View(new User());
        }

        [HttpPost]
        public ActionResult Subscribe(User user)
        {
            string salutation = "";
            if (user.GenderId == 1)
            {
                salutation = "monsieur " + user.LastName;
            }
            else if (user.GenderId == 2)
            {
                salutation = "madame " + user.LastName;
            }
            else if (user.GenderId == 3)
            {
                salutation = user.FirstName;
            }

            ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
            user.CreationDate = DateTime.Now;
            ViewBag.FullName = user.GetFullName();
            ViewBag.Gender = user.Gender;
            if (ModelState.IsValid)
            {
                if (DB.AddUser(user) != null)
                {
                    SendEmailVerification(user, user.Email);
                    return RedirectToAction("SubscribeDone", new { id = user.Id });
                }

                else
                    return RedirectToAction("Report", "Errors", new { message = "Échec de création du user" });
            }
            return View(user);
        }
        public ActionResult SubscribeDone(int id)
        {
            User user = DB.Users.Find(id);

            if (user.GenderId == 1)
            {
                ViewBag.Salutation = "monsieur " + user.LastName;
            }
            else if (user.GenderId == 2)
            {
                ViewBag.Salutation = "madame " + user.LastName;
            }
            else if (user.GenderId == 3)
            {
                ViewBag.Salutation = user.FirstName;
            }

            return View();
        }
        public ActionResult VerifyUser(int userid, int code)
        {
            if (DB.VerifyUser(userid, code))
                return RedirectToAction("VerifyDone", "Accounts", new { userid });
            else
            {
                ViewBag.MessageErreur = "Erreur de confimation d'email";
                return RedirectToAction("VerifyError", "Accounts");
            }
        }
        public ActionResult VerifyDone(int userid)
        {
            User user = DB.Users.Find(userid);

            if (user.GenderId == 1)
            {
                ViewBag.Salutation = "monsieur " + user.LastName;
            }
            else if (user.GenderId == 2)
            {
                ViewBag.Salutation = "madame " + user.LastName;
            }
            else if (user.GenderId == 3)
            {
                ViewBag.Salutation = user.FirstName;
            }

            return View();
        }
        public ActionResult VerifyError()
        {
            return View();
        }
        public ActionResult Profil()
        {
            User user = OnlineUsers.GetSessionUser();
            if (user != null)
            {
                Session["Password"] = user.Password;
                Session["UnchangedPasswordCode"] = Guid.NewGuid().ToString();
                ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
                ViewBag.Gender = user.GenderId;
                return View(user);
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Profil(User user)
        {
            User ancien = DB.FindUser(user.Id);

            if (ModelState.IsValid)
            {
                if (user.AvatarImageData != null || user.GenderId != ancien.GenderId || user.Password != Session["UnchangedPasswordCode"].ToString() && user != null)
                {
                    DB.UpdateUser(user);
                }
                if (user.Email != ancien.Email)
                {
                    SendEmailAfterChange(user, user.Email);
                    return RedirectToAction("ChangeEmail", "Accounts");
                }

                return RedirectToAction("Index", "Movies");
            }

            Session["Password"] = user.Password;
            Session["UnchangedPasswordCode"] = Guid.NewGuid().ToString();
            ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
            ViewBag.Gender = user.GenderId;
            return View(user);
        }
        #endregion
        public ActionResult ChangeEmail()
        {
            return View();
        }
        public ActionResult EmailChanged(int userId, int code, string email)
        {
            if (DB.VerifyUser(userId, code))
            {
                User user = DB.Users.Find(userId);
                user.Email = email;
                user.ConfirmEmail = email;
                DB.UpdateUser(user);
                return RedirectToAction("Logout", "Accounts");
            }
            else
            {
                return RedirectToAction("VerifyError", "Accounts", new { userId, code });
            }
        }
        public ActionResult EmailChangedAlert()
        {
            return View();
        }
        public void SendEmailVerification(User user, string newEmail)
        {
            if (user.Id != 0)
            {
                UnverifiedEmail unverifiedEmail = DB.Add_UnverifiedEmail(user.Id, newEmail);
                if (unverifiedEmail != null)
                {
                    string verificationUrl = Url.Action("VerifyUser", "Accounts", null, Request.Url.Scheme);
                    string Link = @"<br/><a href='" + verificationUrl + "?userid=" + user.Id + "&code=" + unverifiedEmail.VerificationCode + @"' > Confirmez votre inscription...</a>";

                    string suffixe = "";
                    if (user.GenderId == 2)
                    {
                        suffixe = "e";
                    }
                    string Subject = "MDB - Vérification d'inscription...";
                    string Body = "Bonjour " + user.GetFullName(true) + @",<br/><br/>";
                    Body += @"Merci de vous être inscrit" + suffixe + " au site MDB. <br/>";
                    Body += @"Pour utiliser votre compte vous devez confirmer votre inscription en cliquant sur le lien suivant : <br/>";
                    Body += Link;
                    Body += @"<br/><br/>Ce courriel a été généré automatiquement, veuillez ne pas y répondre.";
                    Body += @"<br/><br/>Si vous éprouvez des difficultés ou s'il s'agit d'une erreur, veuillez le signaler à <a href='" + SMTP.OwnerEmail + "'>" + SMTP.OwnerName + "</a> (Webmestre du site MDB)";

                    SMTP.SendEmail(user.GetFullName(), unverifiedEmail.Email, Subject, Body);
                }
            }
        }
        public ActionResult ResetPasswordCommand()
        {
            return View();
        }
        [HttpPost]
        public ActionResult ResetPasswordCommand(string email)
        {
            User user = DB.Users.Where(u => u.Email == email).FirstOrDefault();
            SendEmailResetPassword(user);
            return RedirectToAction("ResetPasswordCommandAlert");
        }
        public ActionResult ResetPasswordCommandAlert()
        {
            return View();
        }
        public ActionResult ResetPassword(int userid, int code)
        {
            if (DB.VerifyUser(userid, code))
            {
                return View();
            }
            return RedirectToAction("ResetPasswordError");
        }

        [HttpPost]
        public ActionResult ResetPassword(int userid, string password)
        {
            if (DB.ResetPassword(userid, password))
                return RedirectToAction("ResetPasswordSuccess");
            return RedirectToAction("ResetPasswordError");
        }
        public ActionResult ResetPasswordError()
        {
            return View();
        }
        public ActionResult ResetPasswordSuccess()
        {
            return View();
        }
        public void SendEmailAfterChange(User user, string newEmail)
        {
            if (user.Id != 0)
            {
                UnverifiedEmail unverifiedEmail = DB.UnverifiedEmails.Where(u => u.UserId == user.Id).FirstOrDefault();
                if (unverifiedEmail == null)
                {
                    unverifiedEmail = DB.Add_UnverifiedEmail(user.Id, newEmail);
                }
                string verificationUrl = Url.Action("EmailChanged", "Accounts", null, Request.Url.Scheme);
                string Link = @"<br/><a href='" + verificationUrl + "?userid=" + user.Id + "&code=" + unverifiedEmail.VerificationCode + "&email=" + unverifiedEmail.Email + @"' > Confirmez votre nouveau courriel...</a>";

                string Subject = "MDB - Vérification d'inscription...";
                string Body = "Bonjour " + user.GetFullName(true) + @",<br/><br/>";
                Body += @"Vous avez modifié votre adresse de courriel.
                        Pour que ce changement soit pris en compte, 
                        vous devez confirmer cette adresse en cliquant sur le lien suivant : <br/><br/>";
                Body += Link;
                Body += @"<br/><br/>Ce courriel a été généré automatiquement, veuillez ne pas y répondre.";
                Body += @"<br/><br/>Si vous éprouvez des difficultés ou s'il s'agit d'une erreur, veuillez le signaler à <a href='" + SMTP.OwnerEmail + "'>" + SMTP.OwnerName + "</a> (Webmestre du site MDB)";

                SMTP.SendEmail(user.GetFullName(), unverifiedEmail.Email, Subject, Body);
            }
        }

        public void SendEmailResetPassword(User user)
        {
            User clone = DB.Users.Where(u => u.Email == user.Email).FirstOrDefault();
            if (user.Id != 0)
            {
                UnverifiedEmail unverifiedEmail = DB.Add_UnverifiedEmail(user.Id, user.Email);
                if (unverifiedEmail != null)
                {
                    string verificationUrl = Url.Action("ResetPassword", "Accounts", null, Request.Url.Scheme);
                    string Link = @"<br/><a href='" + verificationUrl + "?userid=" + user.Id + "&code=" + unverifiedEmail.VerificationCode + @"' > Réinitialiser votre mot de passe...</a>";

                    string Subject = "MDB - Changement de mot de passe...";
                    string Body = "Bonjour " + clone.GetFullName(true) + @",<br/><br/>";
                    Body += @"Vous avez demandé de réinitialiser votre mot de passe. <br/>";
                    Body += @"Procédez en cliquant sur le lien suivant : <br/>";
                    Body += Link;
                    Body += @"<br/><br/>Ce courriel a été généré automatiquement, veuillez ne pas y répondre.";
                    Body += @"<br/><br/>Si vous éprouvez des difficultés ou s'il s'agit d'une erreur, veuillez le signaler à <a href='" + SMTP.OwnerEmail + "'>" + SMTP.OwnerName + "</a> (Webmestre du site MDB)";

                    SMTP.SendEmail(clone.GetFullName(), unverifiedEmail.Email, Subject, Body);
                }
            }
        }
    }
}     
