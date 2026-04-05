# Admin Kullanıcısı Oluşturma

İlk çalıştırmada admin kullanıcısı otomatik oluşturulmaz.
Aşağıdaki adımlarla admin hesabı oluşturun:

## Yöntem 1: Kayıt Sayfasından (Identity varsayılan)
1. Projeyi çalıştırın
2. /Identity/Account/Register adresine gidin
3. Hesap oluşturun
4. MSSQL'de AspNetUsers tablosunda EmailConfirmed = 1 yapın

## Yöntem 2: SQL ile direkt
```sql
-- Uygulamayı bir kere çalıştırın, tablolar oluşsun
-- Sonra Identity scaffolding ile /register sayfasından kaydolun
-- Ya da seed kodunu Program.cs'e ekleyin
```

## Yöntem 3: Hızlı Admin Seed (Program.cs'e ekle)
```csharp
// Program.cs'de app.Run()'dan önce:
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var adminEmail = "admin@brikonyapi.com";
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        await userManager.CreateAsync(admin, "Brikon2024!");
    }
}
```
