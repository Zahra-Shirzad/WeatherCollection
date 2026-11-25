# جمع‌آوری آب‌وهوا

سرویس ثبت درخواست جمع‌آوری آب‌وهوا برای چند موقعیت جغرافیایی، دریافت غیرهم‌زمان از [Open-Meteo](https://api.open-meteo.com/v1/forecast)، و نگهداری نتیجه در SQL Server.

مصرف‌کننده برای ثبت درخواست منتظر سرویس خارجی نمی‌ماند. یک worker پس‌زمینه موقعیت‌ها را مستقل از هم، با سقف همزمانی، پردازش می‌کند.

## پیش‌نیاز

- .NET SDK 10.0.402 یا جدیدتر
- SQL Server محلی، یا Docker برای `docker compose`

## اجرا با Docker

از همین پوشه:

```bash
docker compose up --build
```

API روی `http://localhost:8080` بالا می‌آید. Swagger در `/swagger` است. SQL Server میزبان روی پورت `14333` در دسترس است. با بالا آمدن API، migrationها خودشان اعمال می‌شوند.

## اجرا روی ماشین

Connection string پیش‌فرض در `src/WeatherCollection.API/appsettings.json` با Windows Authentication به `localhost` وصل می‌شود. آن را با نمونه SQL خودتان عوض کنید، بعد:

```bash
dotnet run --project src/WeatherCollection.API
```

اگر دیتابیس هنوز ساخته نشده باشد، هنگام شروع برنامه ساخته می‌شود. برای ساخت دستی migration:

```bash
dotnet ef database update --project src/WeatherCollection.Infrastructure --startup-project src/WeatherCollection.API
```

## API

ثبت درخواست، `202 Accepted` و همان `requestId`:

```http
POST /api/weather-requests
Content-Type: application/json

{
  "requestId": "REQ-10001",
  "locations": [
    { "latitude": 35.6892, "longitude": 51.3890 },
    { "latitude": 48.8566, "longitude": 2.3522 }
  ]
}
```

وضعیت کلی:

```http
GET /api/weather-requests/REQ-10001
```

پاسخ شامل وضعیت (`Pending`، `Processing`، `Completed`، `PartiallyCompleted`، `Failed`)، زمان ثبت و شروع و پایان، و تعداد کل، پردازش‌شده، موفق و ناموفق است.

نتیجه هر موقعیت:

```http
GET /api/weather-requests/REQ-10001/results
```

برای موقعیت ناموفق، وضعیت `Failed` و متن خطا برمی‌گردد و فیلدهای آب‌وهوا خالی می‌مانند.

## تصمیم‌های فنی

ثبت درخواست فقط ردیف‌ها را در SQL Server می‌نویسد و تمام می‌شود. پردازش در `WeatherCollectionWorker` است تا پاسخ ثبت به تأخیر Open-Meteo وابسته نباشد.

هر موقعیت یک ردیف مستقل است. worker یک دسته را با `UPDLOCK, READPAST` claim می‌کند تا دو اجرا همزمان یک موقعیت را برندارند. تعداد آیتم‌های هر دسته `BatchSize` است و تعداد فراخوانی همزمان `MaxConcurrency`. نتیجه هر موقعیت جدا ذخیره می‌شود؛ شکست یکی بقیه را پاک نمی‌کند.

اگر پروسه وسط کار ری‌استارت شود، lease منقضی دوباره claim می‌شود. نتیجه‌ای که قبلاً ذخیره شده سر جایش می‌ماند. تا وقتی همه موقعیت‌ها به وضعیت نهایی نرسیده‌اند، اگر حداقل یک تلاش واقعی انجام شده باشد وضعیت درخواست `Processing` می‌ماند، حتی وقتی تلاش بعدی برای بعد زمان‌بندی شده است.

ارسال مجدد همان `requestId` با همان مختصات و همان ترتیب، درخواست جدیدی نمی‌سازد و همان وضعیت را برمی‌گرداند. اگر مختصات یا ترتیب‌شان فرق کند پاسخ `409 Conflict` است. این قانون با ایندکس یکتای `RequestId` حتی برای درخواست‌های همزمان هم برقرار است. hash از مختصات با ترتیب ورودی ساخته می‌شود.

تلاش مجدد فقط یک بودجه دارد: `MaxAttempts` روی worker، با تأخیر نمایی و jitter. `HttpClient` خودش retry نمی‌کند تا در قطعی سرویس خارجی هر موقعیت چند برابر درخواست نزند. timeout هر فراخوانی ۲۰ ثانیه است. خطای `5xx`، `408`، `429`، timeout و قطع اتصال موقت‌اند. سایر `4xx` و بدنه غیرقابل استفاده دائمی هستند و همان بار اول ناموفق می‌شوند. circuit breaker بعد از خطای متوالی باز می‌شود؛ در آن حالت فراخوانی HTTP انجام نمی‌شود، تلاش از بودجه کم نمی‌شود، و فاصله polling بیشتر می‌شود. با برگشتن سرویس، موقعیت‌های ناتمام ادامه پیدا می‌کنند.

## تست

```bash
dotnet test
```

تست‌ها اعتبارسنجی، idempotency، محاسبه وضعیت، دسته‌بندی خطای Open-Meteo و hash را بدون SQL Server و Docker اجرا می‌کنند.

## پیکربندی worker

در `appsettings.json` بخش `WeatherWorker`:

- `MaxConcurrency`: سقف فراخوانی همزمان
- `BatchSize`: اندازه هر claim
- `PollingIntervalSeconds`: فاصله بیکاری worker
- `MaxAttempts`: سقف تلاش واقعی برای هر موقعیت
- `LeaseDurationSeconds`: مهلت مالکیت یک claim؛ باید از timeout یک فراخوانی بزرگ‌تر بماند
- `DependencyBackoffSeconds`: فاصله انتظار وقتی circuit باز است
