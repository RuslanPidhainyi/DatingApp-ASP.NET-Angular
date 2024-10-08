﻿using System.Security.Cryptography;
using System.Text;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

public class AccountController(UserManager<AppUser> userManager, ITokenService tokenService, IMapper mapper) : BaseApiController
{
   [HttpPost("register")] //account/register
   public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
   {
      if (await UserExists(registerDto.Username)) return BadRequest("Username is taken");

      //Ключове слово using у середині методу використовується для того, щоб вказати на те, що об'єкт, який створюється, повинен бути автоматично знищений після завершення блоку коду, в якому він використовується. Це допомагає звільнити ресурси, що використовуються об'єктом (наприклад, файли, мережеві з'єднання, потоки тощо).(а точніше, буде викликано його метод Dispose) 

      //using var hmac = new HMACSHA512();//algortmu do haszującego tekstu - robi szyfrowanie tekstu 

      var user = mapper.Map<AppUser>(registerDto);

      user.UserName = registerDto.Username.ToLower(); //do BD jest zapisane username mawymi leterammi
                                                      // user.PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDto.Password)); //ComputeHash - metoda pobiera tablice bajtów //Encoding.UTF8.GetBytes(password) - uzycie kodowanie tekstu czyli  utworzylismy tablice bajtów z haslem ktore podane przez User'a.
                                                      // user.PasswordSalt = hmac.Key;

      //Uzywamy sortowanie, aby zakodować już zaszyfrowany hash hasla - to bedzie zrobione dla tego a bys User'y uzywali takiego samego hasla to hash haslo rowniez był inny. Poniewasz jezeli nasza BD zostanie naruszona, kazdy uzytkownik bedzie miał inny skrót hasła(PasswordHash), nawet uzywają tego samego hasla


      //context.Users.Add(user);//przekazujemy do naszego context'a nowego User'a
      //await context.SaveChangesAsync();//zapisujemy zmiany do bd, (czyli stworzenego user'a) - zapisywane zmiany w EF

      var result = await userManager.CreateAsync(user, registerDto.Password);
      
      if (!result.Succeeded) return BadRequest(result.Errors);

      //return user; // po zapisaniu naszego user'a w bd mozemy zwrocic user'a, ktorego wlasnie utworzylismy 
      //Ale tutaj nie zwracamy juz user'a, a zwracamy nowe DTO uzytkownika
      return new UserDto
      {
         Username = user.UserName,
         Token = await tokenService.CreateToken(user),
         KnownAs = user.KnownAs,
         Gender = user.Gender
      };
   }

   [HttpPost("login")]
   public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
   {
      var user = await userManager.Users
      .Include(p => p.Photos)
         .FirstOrDefaultAsync(x =>
            x.NormalizedUserName == loginDto.Username.ToUpper());
            //x.UserName == loginDto.Username.ToLower()); //FirstOrDefaultAsync - abo wysli nam object ktore spewnia kryteria lub zwroci "null" 
      //x.UserName - okreslilismy ze nasze names' user'y będą konvertowane na male litery w naszej BD
      //loginDto.Username.ToLower() dla czego zrobilismy konwertacje na male, aby dopasowac podobne do podobnych

      /*
         Rorwniez mozemy uzyć:
         
         .Where - jesli chcemy uzyc liste userow spelniajacych okreslone kryteria  

         .FirstOrDefaultAsync - jesli uzyjemy tego, to jesli uzytkownik nie zostanie znaleziony, zwroci wartosc domyslną, jesli ten uzytkownik nie zostanie znalezony w naszej bd.
         Domyslana wartosc w tym przypadku bedzie null,  jesli uzytkowni nie istnieje w naszej bd.   

         .SingleOrDefaultAsync - mozemy uzyc pojedynczego, to znajdzie jedynego uzytkownika, ktory istnieje w bazie danych, ale jesli w naszej bd jest więcej niz jeden element, ktory pasuje, to ponownie rzuca wyjątek.
      
      */

      if (user == null || user.UserName == null) return Unauthorized("Invalid username");

      // using var hmac = new HMACSHA512(user.PasswordSalt);
      // var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password));//pobieranie passworda

      // for (int i = 0; i < computedHash.Length; i++)
      // {
      //    if (computedHash[i] != user.PasswordHash[i]) return Unauthorized("Invalid password");
      // }

      var result = await userManager.CheckPasswordAsync(user, loginDto.Password);

      if(!result) return Unauthorized();

      return new UserDto
      {
         Username = user.UserName,
         KnownAs = user.KnownAs,
         Token = await tokenService.CreateToken(user),
         Gender = user.Gender,
         PhotoUrl = user.Photos.FirstOrDefault(x => x.IsMain)?.Url
      };
   }

   private async Task<bool> UserExists(string username)
   {
      return await userManager.Users.AnyAsync(x => x.NormalizedUserName == username.ToUpper()); // Bob != bob
   }
}
