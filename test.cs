using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
class Program { 
    static void Main() { 
        var o = new JwtBearerOptions(); 
        o.MapInboundClaims = false; 
        Console.WriteLine(o.MapInboundClaims); 
    } 
}
