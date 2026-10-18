# تسليم Labaik API لفريق Flutter (18 أكتوبر 2026)

هذا الملف يصف حالة الفرع `main` كما هي منشورة الآن على Render، بعد Batch 2. كل المسارات والحقول والقيم مأخوذة من الكود ومن وثيقة OpenAPI المولّدة منه (`/openapi/v1.json`)، وليست من الذاكرة. عند أي اختلاف، المرجع هو `https://labaik-api.onrender.com/openapi/v1.json`.

---

## 1. التغييرات الكاسرة منذ آخر تسليم (16 أكتوبر)

راجعوا هذه البنود أولاً، فهي تغيّر سلوكاً كان التطبيق يعتمد عليه أو قد يعتمد عليه.

| # | التغيير | ما يلزم في التطبيق |
| --- | --- | --- |
| 1 | `POST /admin/companies` صار يتطلب `commercialRegistrationNumber` و`contactFullName` (إضافة إلى `name` و`ownerEmail`)، ويعيد `AdminCompanyDto` كاملاً مع 201. | يخص لوحة التحكم فقط. |
| 2 | البريد غير المسجّل في `POST /admin/companies` وفي `POST /company/groups/{groupId}/members` لم يعد يعيد 404. صار الخادم ينشئ الحساب ويرسل كلمة مرور مؤقتة بالبريد. | لا تعتمدوا على 404 لمعرفة أن البريد غير موجود. |
| 3 | `PUT /company/groups/{groupId}/supervisor` يقبل فقط مشرفاً موجوداً في قائمة مشرفي الشركة. غير ذلك يعيد 404. | أضيفوا المشرف أولاً عبر `POST /company/supervisors` ثم عيّنوه. |
| 4 | `POST /groups` و`POST /groups/{code}/join` صارا للمسؤول (Admin) فقط، وأي دور آخر يأخذ 403. | التطبيق لا يستدعيهما (الشركة تضيف الأعضاء). |
| 5 | `AnnouncementDto.groupId` صار يقبل `null`: إعلانات الشركة لكل المعتمرين (`audience = 2`) ليس لها مجموعة. | تعاملوا مع `groupId` كحقل اختياري. |
| 6 | مجموعات الشركة تبدأ بلا مشرف، لذلك قد تكون هذه الحقول `null`: `HomeGroupDto.leaderName` و`CompanyGroupDto.supervisorId` و`supervisorName` و`GroupDto.leaderId`. | اعرضوا حالة "لا يوجد مشرف بعد". |
| 7 | رسائل دردشة المجموعة صارت تنشئ إشعارات (النوع 8 والنوع 9)، وأُضيفت أنواع 10 و11 و12. عدد `GET /notifications/unread-count` سيرتفع بسبب ذلك. | عالجوا الأنواع الجديدة، وتجاهلوا أي نوع غير معروف بدل أن يتعطل التطبيق. |
| 8 | `ChatMessageDto.body` قد يكون نصاً فارغاً `""` في رسائل الموقع (`kind = 2`). | اعرضوا بطاقة الموقع بدل النص. |
| 9 | `supervisorsCount` في `GET /company/home` صار عدد المشرفين في قائمة الشركة (معيّنين أو لا)، وليس عدد مشرفي المجموعات فقط. | لا شيء، الرقم يتغير فقط. |
| 10 | `PUT /auth/account-type` غير موجود في الخادم ولن يُبنى. التسجيل العام للمعتمرين فقط. | احذفوا شاشة نوع الحساب واستدعاءها. |
| 11 | مفاتيح `errors` في أخطاء 422 صارت camelCase بمسار JSON مثل جسم الطلب: `fullName` و`arrival.entryPoint` و`stays[0].placeName` (كانت `FullName`). | اربطوا أخطاء الحقول بأسماء camelCase. |

إضافة مهمة غير كاسرة: كل خطأ عمل صار يحمل حقل `code` ثابتاً للآلة (مثل `Group.AlreadyMember`) بجانب `status` و`title` كما هما. التفاصيل في القسم 2.

تغييرات إضافية غير كاسرة (حقول جديدة فقط): `HomeGroupDto.unreadMessages`، وحقول الدردشة `kind` و`latitude` و`longitude` و`senderPhotoUrl` و`isRead`، و`pilgrimageType` الاختياري في `PUT /journeys/{id}`، و`fullName` و`phoneNumber` الاختياريان في إضافة عضو، وحقول إضافية في `GET /company/me`.

---

## 2. أساسيات العقد

### العنوان والإصدار

| البند | القيمة |
| --- | --- |
| Base URL | `https://labaik-api.onrender.com/api/v1` |
| الإصدار | في المسار: `/api/v{version}`، والإصدار الحالي الوحيد هو `1`. |
| التنسيق | JSON بأسماء حقول camelCase. |
| OpenAPI | `https://labaik-api.onrender.com/openapi/v1.json` |
| Scalar | `https://labaik-api.onrender.com/scalar/v1` (مفعّل على النسخة المنشورة) |
| التواريخ | `DateTimeOffset` بصيغة ISO 8601 بتوقيت UTC، مثل `2026-12-10T03:00:00+00:00`. حقول التاريخ فقط (`DateOnly`) بصيغة `yyyy-MM-dd`، مثل `departureDate` و`contractEndDate`. التنسيق الهجري والأرقام العربية مسؤولية التطبيق. |
| القيم التعدادية (enums) | تُرسل وتُستقبل كأرقام صحيحة دائماً، وليس كنصوص. الجداول في القسم 4. |
| المعرّفات | كلها `Guid` كنص. |

### توليد عميل Dart من OpenAPI

- الوثيقة الحية `https://labaik-api.onrender.com/openapi/v1.json` بصيغة **OpenAPI 3.0.4**، ومعها نسخة مطابقة في المستودع `docs/openapi/v1.json`. اختبار في CI يفشل إذا اختلفت النسخة عن الكود.
- لكل عملية `operationId` ثابت (مثل `Login` و`GetHome` و`SendChatMessage`)، ونوع الاستجابة الفعلي ورموز الحالة، وجسم الطلب إن وُجد.
- كل enum رقمي، ومعه `x-enum-varnames` (الأسماء) ووصف مثل `1 = Umrah, 2 = Hajj`، فيولّد المولّد enums صالحة في Dart.
- الحقول التي تقبل `null` معلّمة بـ `nullable: true`.
- كل عملية لها استجابة `default` من نوع `ProblemDetails` (فيها `code` و`traceId`)، وأخطاء 422 من نوع `HttpValidationProblemDetails`.
- ما لا تحمله OpenAPI جيداً (أنواع الإشعارات ودلالاتها، وأحداث SignalR، ورموز الأخطاء) موجود في `docs/contract-extras.json`.
- الأمر المجرّب: `openapi-generator generate -i docs/openapi/v1.json -g dart -o labaik_api` (الإصدار v7.10.0).

### المصادقة

- كل المسارات تتطلب `Authorization: Bearer <accessToken>` ما عدا مسارات `/auth/*` المذكورة كمسارات عامة في القسم 5.
- `accessToken` هو JWT صلاحيته **15 دقيقة** (الإعداد الافتراضي). الرقم الفعلي يأتي في `expiresIn` بالثواني في كل استجابة توكن.
- `refreshToken` صلاحيته **7 أيام**، ويُستخدم **مرة واحدة فقط**: كل `POST /auth/refresh-token` يعيد زوجاً جديداً ويُبطل القديم. إعادة استخدام توكن مستعمل أو منتهٍ تعيد 401 بعنوان `The refresh token is invalid or expired.`، وعندها يجب تسجيل الدخول من جديد.
- `POST /auth/reset-password` يُبطل كل توكنات التحديث للمستخدم.
- محتوى الـ JWT: `sub` (معرّف المستخدم) و`email` و`name`، وادعاءات الأدوار للحسابات التي لها دور. الطريقة الموصى بها لقراءة الأدوار هي `user.roles` في استجابة التوكن، أو `GET /auth/me`.
- تغيّر الأدوار (مثل منح دور مشرف) يظهر من **التوكن التالي**، أي بعد `refresh-token`. عند 403 جدّدوا التوكن وأعيدوا الطلب مرة واحدة (كما يفعل التطبيق الآن).
- قواعد كلمة المرور: 8 أحرف على الأقل، وتحتوي حرفاً كبيراً وحرفاً صغيراً ورقماً ورمزاً. إذا خالفت كلمة المرور قواعد Identity يعود الخطأ 400 والرسالة في `title`.
- رمز التحقق OTP من 6 أرقام، صالح 10 دقائق، 5 محاولات، ولا يُعاد إرساله قبل 55 ثانية (وإلا 429).

### شكل الأخطاء (ProblemDetails, RFC 9457)

خطأ قاعدة العمل يعود بهذا الشكل (`Content-Type: application/problem+json`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The user is already a member of this group.",
  "status": 409,
  "instance": "POST /api/v1/company/groups/7e8f9a0b-1c2d-4e3f-8a5b-6c7d8e9f0a1b/members",
  "code": "Group.AlreadyMember",
  "traceId": "00-2895e5aa0d41af41437e965bf73a1eee-3a607953c8512a5f-00"
}
```

- **`code`** ثابت للآلة بصيغة `Area.Reason`، وهو ما يجب أن يعتمد عليه التطبيق لاختيار الرسالة المترجمة. القائمة الكاملة أدناه. لن تتغير الرموز الموجودة، والجديد يُضاف فقط.
- `title` رسالة إنجليزية للتسجيل وليست للعرض. استثناء تاريخي: الدخول بحساب غير مؤكد يعيد 403 مع `code = Auth.EmailNotConfirmed` و`title = email_not_confirmed`.
- **ردود بلا `code`**: الـ 401 الذي يصدر عن طبقة المصادقة نفسها (توكن مفقود أو منتهٍ، `title = Unauthorized`)، والـ 403 من سياسة الدور (`title = Forbidden`)، والـ 500 (`An unexpected error occurred.`)، وأخطاء 422. عاملوها حسب `status`.
- أخطاء شكل الطلب (FluentValidation) تعود 422 مع قاموس `errors` بلا `code`. **المفاتيح camelCase بمسار JSON مثل جسم الطلب**: `fullName`، و`arrival.entryPoint`، و`stays[0].placeName`، و`days[1].activities[0].title`. الاستثناء الوحيد `destination` في `GET /map/route` (عند غياب `destinationId` و`toLat`/`toLng` معاً).

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "instance": "POST /api/v1/auth/register",
  "errors": {
    "email": ["'Email' is not a valid email address."],
    "password": ["The length of 'Password' must be at least 8 characters. You entered 5 characters."]
  },
  "traceId": "00-..."
}
```

- `traceId` يربط الخطأ بسجلات الخادم. أرسلوه مع أي بلاغ خطأ.
- أخطاء SignalR تصل كـ `HubException` برسالة تبدأ بنفس الرمز، مثل `Location.SharingDisabled: ...`.

### رموز الأخطاء (`code`)

الرقم بين القوسين هو `status`. الرموز من نوع Validation و Failure كلاهما 400.

| العائلة | الرموز |
| --- | --- |
| `Auth` | `Auth.InvalidCredentials` (401)، `Auth.EmailNotConfirmed` (403)، `Auth.InvalidRefreshToken` (401)، `Auth.InvalidGoogleToken` (401)، `Auth.ExternalEmailNotVerified` (401)، `Auth.GoogleNotConfigured` (400)، `Auth.ExternalLinkFailed` (400)، `Auth.ResetFailed` (400)، `Auth.Required` (401) |
| `Otp` | `Otp.Invalid` (400)، `Otp.Expired` (400)، `Otp.NotFound` (400)، `Otp.TooManyAttempts` (400)، `Otp.Throttled` (429) |
| `User` | `User.EmailTaken` (409)، `User.NotFound` (404)، `User.CreationFailed` (400)، `User.RoleChangeFailed` (400) |
| `Company` | `Company.NotFound` (404)، `Company.SupervisorNotFound` (404)، `Company.OwnerAlreadyHasCompany` (409)، `Company.RegistrationNumberTaken` (409)، `Company.SupervisorAlreadyAdded` (409)، `Company.OwnerCannotBeSupervisor` (409)، `Company.NameRequired`، `Company.NameTooLong`، `Company.RegistrationNumberRequired`، `Company.ContactNameRequired`، `Company.TooLong`، `Company.InvalidUrl`، `Company.NegativeNumber`، `Company.ContractEndBeforeStart`، `Company.OwnerRequired`، `Company.SupervisorRequired` (كلها 400) |
| `Trip` | `Trip.NotFound` (404)، `Trip.CannotEditItinerary` (403)، `Trip.NameRequired`، `Trip.DeparturePlaceRequired`، `Trip.DestinationsRequired`، `Trip.InvalidDestination`، `Trip.TooLong`، `Trip.ReturnBeforeDeparture`، `Trip.InvalidExpectedPilgrims`، `Trip.CompanyRequired`، `Trip.ItineraryTooLarge`، `Trip.DuplicateItineraryDay`، `Trip.ItineraryDayOutsideTrip`، `Trip.InvalidActivity` (كلها 400) |
| `Group` | `Group.NotFound` (404)، `Group.MemberNotFound` (404)، `Group.NotLeader` (403)، `Group.NotPilgrim` (403)، `Group.AlreadyMember` (409)، `Group.CannotRemoveSupervisor` (409)، `Group.NameRequired`، `Group.NameTooLong`، `Group.JoinCodeRequired`، `Group.LeaderRequired`، `Group.TripRequired`، `Group.AddressRequired`، `Group.AddressTooLong`، `Group.InvalidCoordinates`، `Group.InvalidAttendanceStatus`، `Group.NotesTooLong` (كلها 400) |
| `Announcement` | `Announcement.NotFound` (404)، `Announcement.NotCompanyOwner` (403)، `Announcement.TitleRequired`، `Announcement.TitleTooLong`، `Announcement.BodyRequired`، `Announcement.BodyTooLong`، `Announcement.InvalidImportance`، `Announcement.LocationTooLong`، `Announcement.InvalidImageUrl` (كلها 400) |
| `Chat` | `Chat.NotMember` (403)، `Chat.BodyRequired`، `Chat.BodyTooLong`، `Chat.InvalidLocation`، `Chat.InvalidKind` (كلها 400) |
| `Notification` | `Notification.NotFound` (404)، `Notification.UserRequired` (400) |
| `Location` | `Location.SharingDisabled` (409)، `Location.InvalidLatitude`، `Location.InvalidLongitude`، `Location.InvalidAccuracy`، `Location.UserRequired` (كلها 400) |
| `Map` | `Map.PlaceNotFound` (404)، `Map.EmergencyNotFound` (404)، `Map.InvalidCoordinates`، `Map.NoteTooLong`، `Map.UserRequired`، `Map.InvalidKey`، `Map.InvalidContent`، `Map.FacilityKindMismatch` (كلها 400) |
| `LostChild` | `LostChild.NotFound` (404)، `LostChild.NotReporter` (403)، `LostChild.InvalidAge`، `LostChild.DescriptionRequired`، `LostChild.TooLong`، `LostChild.InvalidPhotoUrl`، `LostChild.ReporterRequired` (كلها 400) |
| `FamilyCircle` | `FamilyCircle.NotFound` (404)، `FamilyCircle.MemberNotFound` (404)، `FamilyCircle.NotOwner` (403)، `FamilyCircle.AlreadyMember` (409)، `FamilyCircle.Full` (409)، `FamilyCircle.OwnerCannotLeave` (409)، `FamilyCircle.NameRequired`، `FamilyCircle.NameTooLong`، `FamilyCircle.JoinCodeRequired`، `FamilyCircle.UserRequired` (كلها 400) |
| `Journey` | `Journey.NotFound` (404)، `Journey.NotEditable` (409)، `Journey.InvalidTransition` (409)، `Journey.InvalidPilgrimageType`، `Journey.EntryPointRequired`، `Journey.PlaceNameRequired`، `Journey.CityRequired`، `Journey.ArrivalPlanRequired`، `Journey.StayRequired`، `Journey.UserRequired` (كلها 400) |
| `Ritual` | `Ritual.NoSteps` (404)، `Ritual.StepNotFound` (404)، `Ritual.StepOutOfOrder` (409)، `Ritual.AlreadyCompleted` (409)، `Ritual.TitleRequired`، `Ritual.BodyRequired`، `Ritual.InvalidOrder`، `Ritual.UserRequired` (كلها 400) |
| `Guide` | `Guide.NotFound` (404)، `Guide.TitleRequired`، `Guide.BodyRequired`، `Guide.InvalidOrder` (كلها 400) |
| `Profile` | `Profile.InvalidGender`، `Profile.InvalidNationality`، `Profile.InvalidPhotoUrl`، `Profile.HealthNotesTooLong`، `Profile.UserRequired` (كلها 400) |

معظم رموز 400 تسبقها فحوص 422 على شكل الطلب، فلن تظهر إلا نادراً. الرموز التي يحتاج التطبيق التعامل معها فعلياً هي غالباً 401 و403 و404 و409 و429 أعلاه.

### رموز الحالة

| الرمز | المعنى في هذا الـ API |
| --- | --- |
| 200 | نجاح مع جسم. |
| 201 | تم الإنشاء (مثل الرحلات والإعلانات والرسائل والبلاغات). |
| 204 | نجاح بلا جسم (تعليم كمقروء، تأكيد الوصول، إرسال الموقع)، أو "لا توجد قيمة بعد" في `GET .../gathering-point` و`GET /pilgrim/supervisor`. |
| 400 | خطأ قاعدة عمل من نوع Validation داخل النطاق (مثل إحداثيات غير صالحة)، أو رفض Identity لكلمة المرور، أو `Google sign-in is not available.` |
| 401 | لا يوجد توكن أو التوكن غير صالح، أو بيانات دخول خاطئة، أو توكن تحديث غير صالح. |
| 403 | الدور لا يسمح (Admin أو Company أو Supervisor)، أو لست مشرف المجموعة، أو `email_not_confirmed`. |
| 404 | غير موجود، **أو** موجود لكنه ليس لك. لا نكشف وجود موارد الآخرين. |
| 409 | تعارض: عضو مكرر، خطوة مناسك خارج الترتيب، رقم سجل تجاري مكرر، رحلة غير قابلة للتعديل، مشاركة الموقع مطفأة، وغيرها. |
| 422 | شكل الطلب غير صالح (قاموس `errors`). |
| 429 | طلب رمز تحقق قبل انتهاء مهلة 55 ثانية. |
| 500 | خطأ غير متوقع، العنوان `An unexpected error occurred.` |

### الترقيم بالمؤشر (cursor)

يُستخدم في `GET /notifications` و`GET /groups/{groupId}/messages`:

- الترتيب من الأحدث إلى الأقدم.
- `take` افتراضياً 50 وأقصاه 100 (أكثر من ذلك يعيد 422).
- الصفحة التالية: أرسلوا `before` = أقدم طابع وقت في الصفحة الحالية (`createdAtUtc` للإشعارات، `sentAtUtc` للرسائل)، مع ترميزه في الرابط (`Uri.encodeQueryComponent`).
- صفحة أقل من `take` تعني النهاية.

---

## 3. نموذج الحسابات والإنشاء من الأعلى

| الحساب | كيف يُنشأ | الدور في `user.roles` |
| --- | --- | --- |
| معتمر | يسجّل بنفسه من التطبيق (`register` ثم `verify-email`)، أو Google، أو تنشئه الشركة عند إضافته لمجموعة ببريد جديد. | لا شيء (قائمة فارغة) |
| شركة | ينشئها المسؤول من لوحة التحكم الويب (`POST /admin/companies`). | `Company` |
| مشرف | تنشئه الشركة (`POST /company/supervisors`) فيدخل قائمة مشرفيها. | `Supervisor` |
| مسؤول | من إعدادات الخادم (`Admin:Emails`). | `Admin` |

### تدفق كلمة المرور المؤقتة

عند إنشاء حساب من الأعلى (شركة أو مشرف أو معتمر تضيفه الشركة):

1. إذا كان **البريد جديداً**: ينشئ الخادم الحساب مؤكداً مباشرة (`EmailConfirmed = true`، بلا OTP)، بالدور المناسب، ويرسل بريداً فيه البريد الإلكتروني وكلمة مرور مؤقتة.
2. إذا كان **البريد مسجلاً ومؤكداً**: يُربط الحساب كما هو (يُضاف الدور إن لزم) ويُرسل إشعار بالبريد بلا كلمة مرور. كلمة مروره الحالية لا تتغير. (المعتمر المسجل الذي تضيفه الشركة لمجموعة لا يصله بريد.)
3. إذا كان **البريد مسجلاً لكن غير مؤكد**: يُعامل كحساب جديد، فتُستبدل كلمة مروره بكلمة مؤقتة تُرسل للبريد الحقيقي، ويُؤكد الحساب، وتُبطل جلساته القديمة.

بعدها يدخل المستخدم عادياً عبر `POST /auth/login` بكلمة المرور المؤقتة. لتغييرها يستخدم مسار نسيت كلمة المرور الحالي (`forgot-password` ثم `reset-password`). لا يوجد إجبار على التغيير عند أول دخول حالياً (مذكور كمهمة قبل الإنتاج). الدور الجديد لمستخدم كان مسجلاً يظهر بعد تجديد التوكن.

---

## 4. القيم التعدادية (enums)

كلها أرقام صحيحة. قيمة جديدة تُضاف دائماً برقم جديد ولا يُعاد ترقيم القديم.

### الإشعارات `NotificationType`

| القيمة | الاسم | المستلم | `referenceId` | `preview` |
| --- | --- | --- | --- | --- |
| 1 | `AnnouncementPosted` | أعضاء المجموعة عدا الكاتب، أو كل معتمري الشركة لإعلان الشركة | معرّف الإعلان | العنوان |
| 2 | `MemberJoinedGroup` | مشرف المجموعة | معرّف العضو | اسم العضو |
| 3 | `GroupCreated` | حساب الشركة | معرّف المجموعة | اسم المجموعة |
| 4 | `SupervisorActivated` | حساب الشركة | معرّف المشرف | اسم المشرف |
| 5 | `GroupWithoutSupervisor` | حساب الشركة (عند أول معتمر في مجموعة بلا مشرف، مرة واحدة) | معرّف المجموعة | اسم المجموعة |
| 6 | `GatheringPointChanged` | أعضاء المجموعة عدا من غيّرها | معرّف المجموعة | العنوان |
| 7 | `PilgrimArrived` | مشرف المجموعة | معرّف المعتمر | اسم المعتمر |
| 8 | `GroupMessagePosted` | أعضاء المجموعة عدا المرسل، عندما يكتب معتمر في دردشة المجموعة | معرّف الرسالة | نص الرسالة (أو `null` لموقع بلا تعليق) |
| 9 | `SupervisorMessagePosted` | معتمرو المجموعة، عندما يكتب المشرف في دردشة المجموعة | معرّف الرسالة | نص الرسالة |
| 10 | `EmergencyReported` | مشرفو مجموعات المعتمر | معرّف بلاغ الطوارئ | اسم المعتمر |
| 11 | `LostChildReported` | مشرفو المبلّغ، ومع `shareWithNearby` كل من يشارك موقعاً حديثاً ضمن 1 كم | معرّف البلاغ | وصف الطفل |
| 12 | `FamilyMemberJoined` | مالك دائرة العائلة | معرّف الدائرة | اسم العضو الجديد |

ملاحظة: القيمة 12 أُضيفت مع دائرة العائلة في B6، فالقائمة تصل إلى 12 وليس 11. ربط شاشة إشعارات المعتمر الأربعة: إعلان جديد = 1، رسالة جديدة من المشرف = 9، تغيير نقطة التجمع = 6، رسالة جديدة للمجموعة = 8. النوع 9 يعني حالياً رسالة المشرف في دردشة المجموعة، وسيُعاد استخدامه للرسائل الفردية عندما تُبنى. `preview` و`groupId` و`referenceId` قد تكون `null`. النصوص المعروضة يبنيها التطبيق من النوع.

### الشركة ولوحة التحكم

| enum | القيم |
| --- | --- |
| `CompanyCategory` | 1 `Umrah`، 2 `Hajj`، 3 `HajjAndUmrah` |
| `SubscriptionPeriod` | 1 `Month`، 2 `ThreeMonths`، 3 `SixMonths`، 4 `Year` |
| `CompanyStatus` (محسوب، لا يُرسل) | 1 `Active`، 2 `Stopped` (أوقفها المسؤول)، 3 `Expired` (انتهى `contractEndDate`) |
| `TripStatus` (محسوب) | 1 `Upcoming`، 2 `Departed` (اليوم بتوقيت UTC يساوي تاريخ الذهاب أو بعده) |

### المجموعات والأعضاء

| enum | القيم |
| --- | --- |
| `GroupMemberRole` | 1 `Leader` (المشرف)، 2 `Member` (معتمر) |
| `AttendanceStatus` | 1 `Present`، 2 `Absent` (الافتراضي) |
| `Gender` | 1 `Male`، 2 `Female` |

### الدردشة والإعلانات

| enum | القيم |
| --- | --- |
| `ChatMessageKind` | 1 `Text`، 2 `Location` |
| `AnnouncementImportance` | 1 `Normal`، 2 `Important` |
| `AnnouncementAudience` | 1 `Group`، 2 `AllCompanyPilgrims` |

### الرحلة الشخصية والمناسك والأدلة

| enum | القيم |
| --- | --- |
| `PilgrimageType` | 1 `Umrah`، 2 `Hajj` |
| `TravelParty` | 1 `Solo`، 2 `Family`، 3 `Group` |
| `TransportMode` | 1 `Flight`، 2 `Bus`، 3 `PrivateCar` |
| `AccommodationType` | 1 `Hotel`، 2 `Other` |
| `JourneyStatus` | 1 `Draft`، 2 `Ready`، 3 `Active`، 4 `Completed` |
| `RitualPhase` | 1 `Preparation`، 2 `Ihram`، 3 `Tawaf`، 4 `Sai`، 5 `HalqOrTaqsir`، 6 `Completion` |
| `RitualStepKind` | 1 `Pillar`، 2 `Obligation`، 3 `Sunnah`، 4 `Guidance` |
| `GuideCategory` | 1 `IhramPreparation` |

### الميدان والمساعدة والعائلة

| enum | القيم |
| --- | --- |
| `MapPlaceType` | 1 `MainGate`، 2 `SubGate`، 3 `Facility` |
| `FacilityKind` (للمرافق فقط) | 1 `Restrooms`، 2 `Medical`، 3 `ZamzamWater`، 4 `LostAndFound`، 5 `WheelchairService` |
| `EmergencyStatus` | 1 `Open`، 2 `Resolved` |
| `LostChildReportStatus` | 1 `Open`، 2 `Found` |
| `FamilyCircleRole` | 1 `Owner`، 2 `Member` |

---

## 5. المسارات

كل المسارات تحت `/api/v1`. الصلاحية:
- **عام**: بلا توكن.
- **مسجل**: أي توكن صالح.
- **عضو**: عضو في المجموعة أو الدائرة. غير العضو يأخذ 404.
- **مشرف المجموعة**: `LeaderId` للمجموعة. العضو العادي يأخذ 403 وغير العضو 404.
- **Company / Supervisor / Admin**: سياسة الدور. سياسة `Supervisor` تقبل المسؤول أيضاً.

الحقول الاختيارية معلّمة بـ `?`. أشكال الـ DTO الكاملة تحت كل جدول.

### 5.1 المصادقة `/auth`

| Method | Path | الصلاحية | Body | الاستجابة |
| --- | --- | --- | --- | --- |
| POST | `/auth/register` | عام | `{ fullName, email, password }` | 200 `{ message }`، ويُرسل OTP. إعادة التسجيل ببريد غير مؤكد ترسل رمزاً جديداً (ليس 409). البريد المؤكد 409. |
| POST | `/auth/verify-email` | عام | `{ email, code }` | 200 `AuthTokens` |
| POST | `/auth/login` | عام | `{ email, password }` | 200 `AuthTokens`. غير مؤكد: 403 `email_not_confirmed`. خطأ: 401. |
| POST | `/auth/google` | عام | `{ idToken }` | 200 `AuthTokens`. إذا لم تُضبط Client IDs على الخادم: 400. |
| POST | `/auth/refresh-token` | عام | `{ refreshToken }` | 200 `AuthTokens` (زوج جديد، القديم يبطل) |
| POST | `/auth/resend-code` | عام | `{ email }` | 200 `{ message }` دائماً، أو 429 قبل 55 ثانية |
| POST | `/auth/forgot-password` | عام | `{ email }` | 200 `{ message }` دائماً (لا يكشف وجود البريد) |
| POST | `/auth/reset-password` | عام | `{ email, code, newPassword }` | 200 `{ message }` |
| GET | `/auth/me` | مسجل | | 200 `{ id, email, name, roles: string[] }` |

```
AuthTokens { accessToken, refreshToken, tokenType: "Bearer", expiresIn: int (seconds), user: AuthUserSummary }
AuthUserSummary { id, email, fullName, roles: string[] }   // roles: [] للمعتمر، أو "Admin" / "Supervisor" / "Company"
```

### 5.2 لوحة تحكم المسؤول `/admin` (Admin فقط)

| Method | Path | Body | الاستجابة |
| --- | --- | --- | --- |
| GET | `/admin/overview` | | 200 `AdminOverviewDto` |
| GET | `/admin/companies` | | 200 `AdminCompanyListItemDto[]` (الأحدث أولاً) |
| POST | `/admin/companies` | `CreateCompanyCommand` | 201 `AdminCompanyDto`. 409 رقم سجل مكرر أو الحساب يملك شركة. |
| GET | `/admin/companies/{id}` | | 200 `AdminCompanyDto` |
| PUT | `/admin/companies/{id}` | `UpdateCompanyRequest` (نفس النموذج بلا `ownerEmail`، استبدال كامل) | 200 `AdminCompanyDto` |
| POST | `/admin/companies/{id}/stop` | | 200 `AdminCompanyDto` (الحالة 2) |
| POST | `/admin/companies/{id}/resume` | | 200 `AdminCompanyDto` |
| GET | `/admin/users?email=` | | 200 `UserAccountDto` |
| PUT | `/admin/users/{userId}/supervisor` | | 200 `UserAccountDto` (يمنح دور Supervisor) |
| DELETE | `/admin/users/{userId}/supervisor` | | 200 `UserAccountDto` |

```
CreateCompanyCommand {
  name, commercialRegistrationNumber, contactFullName, ownerEmail,        // إلزامية
  category?, officialEmail?, primaryPhone?, logoUrl? (https), country?, city?,
  vatId?, contractReferenceNumber?, subscriptionPrice? (>= 0), subscriptionPeriod?,
  contractStartDate? (yyyy-MM-dd), contractEndDate? (>= start), maxSupervisors?, maxPilgrims?,
  contractFileUrl? (https), contactJobTitle?, contactMobile?,
  enabledServices? { gpsTracking, duaAndRituals, instantNotifications, chatWithGuides, reportsAndStats }  // إن حُذف: كلها true
}
AdminCompanyDto { id, name, ownerUserId, ownerEmail, createdAtUtc, + كل حقول النموذج أعلاه (بلا ownerEmail),
  enabledServices, isStopped, status: CompanyStatus, daysRemaining?: int,
  stats { tripsCount, activeTripsCount, groupsCount, supervisorsCount, activePilgrims } }
AdminCompanyListItemDto { id, name, logoUrl?, activePilgrims, subscriptionStartUtc? (yyyy-MM-dd), subscriptionEndUtc? (yyyy-MM-dd), status, daysRemaining? }
AdminOverviewDto { pendingRequests (0 حالياً), activeSubscriptions, subscriptionsExpiringIn30Days, activePilgrims, activeCompanies, totalCompanies }
UserAccountDto { id, email, fullName, emailConfirmed, roles: string[] }
```

`daysRemaining` = الأيام حتى `contractEndDate` (`null` بلا تاريخ، و0 بعد الانتهاء). "معتمر نشط" = عضو في مجموعة شركة رحلتها لم تنته. الفوترة والحدود وعلامات الخدمات مخزنة فقط ولا تُطبّق.

### 5.3 عمليات الشركة `/company` (Company فقط)

| Method | Path | Body | الاستجابة |
| --- | --- | --- | --- |
| GET | `/company/me` | | 200 `CompanyDto` |
| PUT | `/company/me` | `{ name, commercialRegistrationNumber, contactFullName, logoUrl?, country?, city?, officialEmail?, primaryPhone? }` | 200 `CompanyDto` (حقول العقد للمسؤول فقط) |
| GET | `/company/home` | | 200 `{ activeTrips, pilgrimsCount, supervisorsCount, groupsCount }` |
| POST | `/company/supervisors` | `{ fullName, email, phoneNumber? }` | 201 `CompanySupervisorDto`. 409 إذا كان في القائمة أو كان حساب الشركة نفسه. |
| GET | `/company/supervisors` | | 200 `CompanySupervisorDto[]` (حسب الاسم) |
| POST | `/company/trips` | `CreateTripCommand` | 201 `TripDto` |
| GET | `/company/trips` | | 200 `TripSummaryDto[]` (الأقرب موعداً أولاً) |
| GET | `/company/trips/{id}` | | 200 `TripDto` |
| PUT | `/company/trips/{id}` | نفس حقول الإنشاء | 200 `TripDto` |
| POST | `/company/trips/{tripId}/groups` | `{ name }` | 201 `CompanyGroupDto` (بلا مشرف) |
| GET | `/company/trips/{tripId}/groups` | | 200 `CompanyGroupDto[]` |
| POST | `/company/groups/{groupId}/members` | `{ email, fullName?, phoneNumber? }` | 200 `CompanyGroupDto`. بريد جديد = حساب معتمر جديد. 409 إذا كان عضواً. |
| GET | `/company/groups/{groupId}/members` | | 200 `GroupMemberRowDto[]` (بلا ملاحظات صحية) |
| GET | `/company/groups/{groupId}/members/{userId}` | | 200 `GroupMemberDetailsDto` (يتضمن الملاحظات الصحية) |
| DELETE | `/company/groups/{groupId}/members/{userId}` | | 204. لا يمكن حذف المشرف (409). |
| PUT | `/company/groups/{groupId}/supervisor` | `{ email }` | 200 `CompanyGroupDto`. من قائمة المشرفين فقط، وإلا 404. يستبدل المشرف السابق. |
| POST | `/company/announcements` | `{ title, body, importance?, gatheringTimeUtc?, location?, imageUrl? }` | 201 `AnnouncementDto` (`audience = 2`، لكل معتمري الشركة) |

```
CompanyDto { id, name, logoUrl?, commercialRegistrationNumber, country?, city?, officialEmail?, primaryPhone?, contactFullName }
CompanySupervisorDto { userId, fullName, email, phoneNumber?, addedAtUtc, groupsCount }
CreateTripCommand { name, pilgrimageType, destinations: string[], departureDate (yyyy-MM-dd), returnDate?, departurePlace, expectedPilgrims (>= 1), notes? }
TripDto { id, name, pilgrimageType, destinations, departureDate, returnDate?, departurePlace, expectedPilgrims, notes?, status: TripStatus, createdAtUtc }
TripSummaryDto { id, name, pilgrimageType, departureDate, returnDate?, expectedPilgrims, status }
CompanyGroupDto { id, tripId, name, joinCode, supervisorId?, supervisorName?, pilgrimCount }
```

### 5.4 ميزات المشرف

| Method | Path | الصلاحية | Body | الاستجابة |
| --- | --- | --- | --- | --- |
| GET | `/supervisor/groups` | Supervisor | | 200 `SupervisorGroupDto[]` (الأقرب موعداً أولاً) |
| GET | `/home?groupId=` | عضو | | يختار المجموعة في الرئيسية (انظر 5.5) |
| PUT | `/groups/{groupId}/gathering-point` | مشرف المجموعة | `{ address, latitude, longitude, gatheringTimeUtc? }` | 200 `GatheringPointDto`، ويرسل إشعار 6 |
| GET | `/groups/{groupId}/gathering-point` | عضو | | 200 `GatheringPointDto` أو 204 إذا لم تُحدد |
| GET | `/groups/{groupId}/attendance` | مشرف المجموعة | | 200 `GroupAttendanceDto` |
| PUT | `/groups/{groupId}/attendance/{userId}` | مشرف المجموعة | `{ status }` (1 أو 2) | 200 `GroupAttendanceDto` |
| POST | `/groups/{groupId}/attendance/reset` | مشرف المجموعة | | 200 `GroupAttendanceDto` (الكل غائب) |
| POST | `/groups/{groupId}/attendance/confirm` | معتمر في المجموعة | | 204، مرة واحدة، يرسل إشعار 7 للمشرف |
| GET | `/groups/{groupId}/members` | مشرف المجموعة | | 200 `GroupMemberRowDto[]` |
| GET | `/groups/{groupId}/members/{userId}` | مشرف المجموعة | | 200 `GroupMemberDetailsDto` |
| PUT | `/groups/{groupId}/members/{userId}/notes` | مشرف المجموعة | `{ notes? }` (`null` يمسح) | 204 |
| POST | `/groups/{groupId}/announcements` | مشرف المجموعة | `{ title, body, importance?, gatheringTimeUtc?, location?, imageUrl? }` | 201 `AnnouncementDto` |
| GET | `/trips/{id}/itinerary` | الشركة، ومشرفو ومعتمرو مجموعات الرحلة | | 200 `TripItineraryDto` |
| PUT | `/trips/{id}/itinerary` | الشركة أو مشرف في الرحلة (المعتمر 403) | `{ days: ItineraryDayDto[] }` | 200 `TripItineraryDto` (استبدال كامل) |

```
SupervisorGroupDto { id, name, tripId?, tripName?, departureDate?, pilgrimCount, presentCount, absentCount }
GatheringPointDto { address, latitude, longitude, gatheringTimeUtc?, updatedAtUtc }
GroupAttendanceDto { presentCount, absentCount, members: [ { userId, fullName?, status: AttendanceStatus, confirmedAtUtc? } ] }
GroupMemberRowDto { userId, fullName?, email?, role, memberNumberInGroup?, gender?, nationality?, photoUrl?, attendanceStatus, joinedAtUtc }
GroupMemberDetailsDto { userId, fullName?, email?, role, memberNumberInGroup?, gender?, nationality?, photoUrl?, healthNotes?, notes?, attendanceStatus, joinedAtUtc }
TripItineraryDto { tripId, tripName, days: ItineraryDayDto[], updatedAtUtc? }
ItineraryDayDto { date (yyyy-MM-dd), title?, activities: [ { title, timeUtc?, place? } ] }
```

`healthNotes` بيانات حساسة: لا تظهر إلا في `GroupMemberDetailsDto` لمشرف المجموعة وللشركة المالكة. أيام البرنامج يجب أن تقع بين تاريخ الذهاب والعودة، بلا تكرار، وبحد 60 يوماً و30 نشاطاً في اليوم.

### 5.5 المعتمر: الرئيسية والرحلة الشخصية والمناسك والأدلة والملف الشخصي

| Method | Path | Body / Query | الاستجابة |
| --- | --- | --- | --- |
| GET | `/home` | `?groupId=` اختياري (عضو فقط، وإلا 404) | 200 `HomeDto` |
| POST | `/journeys` | `CreateJourneyCommand` | 201 `{ id }` |
| GET | `/journeys/me` | | 200 `JourneyDto` أو 404 إذا لا توجد |
| GET | `/journeys/{id}` | | 200 `JourneyDto` (المالك فقط) |
| PUT | `/journeys/{id}` | `UpdateJourneyRequest` | 200 `JourneyDto`. مسموح في Draft وReady فقط، بعدها 409. |
| GET | `/rituals/steps?pilgrimageType=` | الافتراضي 1 | 200 `RitualStepDto[]` |
| GET | `/rituals/progress?pilgrimageType=` | يبدأ التقدم عند أول طلب | 200 `RitualProgressDto` |
| POST | `/rituals/progress/complete` | `{ pilgrimageType, stepOrder }` | 200 `RitualProgressDto`. بالترتيب فقط، وإلا 409. |
| POST | `/rituals/progress/reset` | `{ pilgrimageType }` | 200 `RitualProgressDto` |
| GET | `/guides?category=` | الافتراضي 1 | 200 `GuideArticleDto[]` |
| GET | `/guides/{id}` | | 200 `GuideArticleDto` |
| GET | `/me/profile` | | 200 `MyProfileDto` |
| PUT | `/me/profile` | `{ gender?, nationality? (رمز دولة من حرفين), photoUrl? (https), healthNotes? }` | 200 `MyProfileDto` (استبدال كامل، `null` يمسح) |

```
HomeDto { journey?: { id, pilgrimageType, status, isFirstTime },
          ritual?: { pilgrimageType, isStarted, isCompleted, completedCount, totalSteps, nextStep?: RitualStepDto },
          group?: { id, name, leaderName?, memberCount, myRole, unreadAnnouncements, tripId?, unreadMessages },
          unreadNotifications }
CreateJourneyCommand { pilgrimageType, isFirstTime, travelParty, arrival: { mode, entryPoint }, stays: [ { cityName, accommodationType, placeName, arrivalAtUtc } ] }
UpdateJourneyRequest { isFirstTime, travelParty, arrival, stays, pilgrimageType? }   // pilgrimageType إن حُذف يبقى كما هو
JourneyDto { id, pilgrimageType, isFirstTime, travelParty, status, arrivalPlan?: { mode, entryPoint }, stays: [ { cityName, accommodationType, placeName, arrivalAtUtc } ] }
RitualStepDto { id, pilgrimageType, order, phase, kind, title, body, reference? }
RitualProgressDto { id, pilgrimageType, currentOrder, totalSteps, completedCount, isCompleted, startedAtUtc, completedAtUtc?, currentStep?, completedSteps: [ { stepOrder, completedAtUtc } ] }
GuideArticleDto { id, category, order, title, body, reference? }
MyProfileDto { gender?, nationality?, photoUrl?, healthNotes?, updatedAtUtc? }
```

أي قسم في `HomeDto` يكون `null` إذا لم ينطبق (لا رحلة، لا مجموعة، لا خطوات منشورة). المجموعة الافتراضية هي آخر مجموعة انضم إليها. نوع المناسك في الرئيسية يتبع نوع الرحلة الشخصية.

### 5.6 الإعلانات

| Method | Path | الصلاحية | الاستجابة |
| --- | --- | --- | --- |
| GET | `/groups/{groupId}/announcements` | عضو | 200 `AnnouncementDto[]` (الأحدث أولاً، يشمل إعلانات الشركة لكل المعتمرين) |
| POST | `/groups/{groupId}/announcements` | مشرف المجموعة | 201 `AnnouncementDto` |
| POST | `/company/announcements` | Company | 201 `AnnouncementDto` |
| POST | `/announcements/{id}/read` | من يستطيع رؤيته | 204 (مكرر بلا أثر) |

```
AnnouncementDto { id, groupId?, title, body, authorId, authorName?, createdAtUtc, isRead,
                  importance: AnnouncementImportance, audience: AnnouncementAudience, gatheringTimeUtc?, location?, imageUrl? }
```

### 5.7 دردشة المجموعة (أعضاء المجموعة)

| Method | Path | Body / Query | الاستجابة |
| --- | --- | --- | --- |
| POST | `/groups/{groupId}/messages` | `{ body?, kind?, latitude?, longitude? }` | 201 `ChatMessageDto` |
| GET | `/groups/{groupId}/messages` | `?before=&take=` | 200 `ChatMessageDto[]` (الأحدث أولاً) |
| POST | `/groups/{groupId}/messages/read` | | 204: كل ما قبل الآن صار مقروءاً لي |
| GET | `/groups/{groupId}/messages/unread-count` | | 200 `{ count }` |

- `kind` محذوف = 1 نص، و`body` إلزامي (حتى 2000 حرف).
- `kind = 2` موقع: `latitude` و`longitude` إلزاميان، و`body` تعليق اختياري (يعود `""` إذا لم يُرسل).
- `isRead` يخص المستخدم الحالي: رسائله دائماً مقروءة، ورسائل غيره مقروءة إذا أُرسلت قبل آخر `messages/read` له أو معه.
- مؤشر القراءة لا يرجع للخلف. عدد غير المقروء يظهر أيضاً في `home.group.unreadMessages`.
- الإرسال يتم عبر REST فقط، والاستقبال الفوري عبر `/hubs/events` (حدث `MessageReceived`).

```
ChatMessageDto { id, groupId, senderId, senderName?, body, sentAtUtc, kind: ChatMessageKind, latitude?, longitude?, senderPhotoUrl?, isRead }
```

### 5.8 الإشعارات (مسجل)

| Method | Path | الاستجابة |
| --- | --- | --- |
| GET | `/notifications?before=&take=` | 200 `NotificationDto[]` (الأحدث أولاً) |
| GET | `/notifications/unread-count` | 200 `{ count }` |
| POST | `/notifications/{id}/read` | 204 |
| POST | `/notifications/read-all` | 204 |

```
NotificationDto { id, type: NotificationType, groupId?, referenceId?, preview?, createdAtUtc, isRead }
```

الإشعارات داخل التطبيق فقط، وتصل فوراً عبر `NotificationReceived` في `/hubs/events`. لا يوجد إشعار دفع والتطبيق مغلق (انظر القسم 8).

### 5.9 الموقع (مسجل)

| Method | Path | Body | الاستجابة |
| --- | --- | --- | --- |
| GET | `/location/me` | | 200 `MyLocationSharingDto` |
| PUT | `/location/sharing` | `{ isSharing }` | 200 `MyLocationSharingDto`. الإيقاف يمسح آخر موقع. |
| POST | `/location` | `{ latitude, longitude, accuracyMeters? }` | 204. إذا كانت المشاركة مطفأة: 409. |
| POST | `/map/location` | نفس الجسم | نفس السلوك تماماً (اسم بديل) |
| GET | `/groups/{groupId}/locations` | | 200 `MemberLocationDto[]` (أعضاء فقط) |

```
MyLocationSharingDto { isSharing, latitude?, longitude?, accuracyMeters?, updatedAtUtc?, isStale }
MemberLocationDto { userId, fullName?, role, isSharing, latitude?, longitude?, accuracyMeters?, updatedAtUtc?, isStale }
```

يُحفظ آخر موقع فقط (بلا سجل). الموقع الأقدم من 15 دقيقة يُعلّم `isStale = true`.

### 5.10 وضع الميدان والخريطة `/map` (مسجل)

| Method | Path | Body / Query | الاستجابة |
| --- | --- | --- | --- |
| GET | `/map/destinations?type=` | `type` اختياري (1 أو 2 أو 3) | 200 `MapPlaceDto[]` (حسب النوع ثم ترتيب البيانات) |
| GET | `/map/route` | `fromLat, fromLng` و(`destinationId` أو `toLat` + `toLng`) | 200 `MapRouteDto`. معرّف غير موجود 404، ونقص الوجهة 422. |
| POST | `/map/emergency` | `{ latitude, longitude, accuracyMeters?, note? (حتى 500) }` | 201 `EmergencyAlertDto`، ويرسل إشعار 10 لمشرفي المعتمر |
| GET | `/map/emergency/{id}` | | 200 `EmergencyAlertDto` (المبلّغ أو أحد مشرفيه فقط) |
| POST | `/map/emergency/{id}/resolve` | | 200 `EmergencyAlertDto` (مكرر بلا أثر) |
| POST | `/map/location` | انظر 5.9 | 204 |

```
MapPlaceDto { id, key, type: MapPlaceType, facilityKind?, name (عربي), number?, description? (عربي), latitude, longitude, imageUrl? }
MapRouteDto { from: { latitude, longitude }, to: { latitude, longitude }, destination?: MapPlaceDto, distanceMeters, bearingDegrees (0 شمال، 90 شرق), walkingMinutes }
EmergencyAlertDto { id, userId, fullName?, latitude, longitude, accuracyMeters?, note?, status: EmergencyStatus, createdAtUtc, resolvedAtUtc? }
```

`/map/route` يعطي إرشاداً بخط مستقيم فقط (المسافة والاتجاه ووقت المشي بسرعة 1.2 م/ث). رسم المسار والتوجيه خطوة بخطوة مسؤولية مكتبة الخرائط في التطبيق. `imageUrl` حالياً `null` لكل الأماكن، فاستخدموا صور التطبيق. `key` ثابت (مثل `gate-king-abdulaziz`) ويصلح لربط الصور محلياً.

### 5.11 مركز المساعدة (مسجل)

| Method | Path | Body / Query | الاستجابة |
| --- | --- | --- | --- |
| GET | `/pilgrim/supervisor` | `?groupId=` اختياري | 200 `SupervisorContactDto`، أو 204 إذا لا يوجد مشرف، أو 404 لمجموعة لست فيها |
| POST | `/lost-child-reports` | `ReportLostChildCommand` | 201 `LostChildReportDto`، ويرسل إشعار 11 |
| GET | `/lost-child-reports/mine` | | 200 `LostChildReportDto[]` (الأحدث أولاً) |
| GET | `/lost-child-reports/{id}` | | 200 للمبلّغ أو لمن وصله الإشعار 11، وإلا 404 |
| POST | `/lost-child-reports/{id}/found` | | 200 `LostChildReportDto` (المبلّغ فقط، غيره ممن وصله الإشعار 403) |

```
SupervisorContactDto { groupId, groupName, supervisorId, fullName?, email?, phoneNumber?, photoUrl? }
ReportLostChildCommand { approxAge (0 إلى 17), description (حتى 1000), photoUrl? (https), latitude, longitude, locationDescription? (حتى 300), shareWithNearby (افتراضياً false) }
LostChildReportDto { id, reporterId, reporterName?, approxAge, description, photoUrl?, latitude, longitude, locationDescription?, shareWithNearby, status: LostChildReportStatus, createdAtUtc, foundAtUtc? }
```

"القريبون" هم من يشاركون موقعاً حديثاً (أقل من 15 دقيقة) على بعد 1 كم أو أقل من مكان البلاغ.

### 5.12 دائرة العائلة `/family-circles` (مسجل، مستقلة عن مجموعات الشركة)

| Method | Path | Body | الاستجابة |
| --- | --- | --- | --- |
| POST | `/family-circles` | `{ name }` (حتى 100) | 201 `FamilyCircleDto` (أنت المالك، شارك `joinCode`) |
| GET | `/family-circles/mine` | | 200 `FamilyCircleSummaryDto[]` |
| GET | `/family-circles/preview/{code}` | | 200 `FamilyCirclePreviewDto` (شاشة التأكيد) |
| POST | `/family-circles/join` | `{ code }` | 200 `FamilyCircleDto`. عضو مسبقاً أو الدائرة ممتلئة (20): 409. رمز خاطئ: 404. |
| GET | `/family-circles/{id}` | | 200 `FamilyCircleDto` (أعضاء فقط) |
| GET | `/family-circles/{id}/locations` | | 200 `FamilyMemberLocationDto[]` |
| PUT | `/family-circles/{id}/location-sharing` | `{ isSharing }` | 200 `{ circleId, isSharingLocation }` |
| DELETE | `/family-circles/{id}/members/{userId}` | | 204. المالك يحذف غيره، والعضو يغادر بمعرّفه، والمالك لا يغادر (409). |

```
FamilyCircleSummaryDto { id, name, joinCode, myRole: FamilyCircleRole, memberCount }
FamilyCircleDto { id, name, joinCode, ownerId, myRole, members: [ { userId, fullName?, photoUrl?, role, isSharingLocation, joinedAtUtc } ] }
FamilyCirclePreviewDto { id, name, ownerName?, memberCount }
FamilyMemberLocationDto { userId, fullName?, isSharing, latitude?, longitude?, accuracyMeters?, updatedAtUtc?, isStale }
```

يظهر موقع العضو في الدائرة فقط إذا فعّل المشاركة **في هذه الدائرة** (`location-sharing`) **و**كانت مشاركته العامة مفعلة (`PUT /location/sharing`) وأرسل موقعاً. الرموز بأحرف كبيرة (6 خانات) والبحث لا يميز حالة الأحرف. زر "عرض الموقع" = `GET /family-circles/{id}/locations` مع `/hubs/location`. زر "بدء محادثة" لا يقابله شيء في الخادم حالياً (الدردشة الفردية مؤجلة).

### 5.13 مسارات المسؤول القديمة للمجموعات (لا يستدعيها التطبيق)

| Method | Path | الصلاحية | الاستجابة |
| --- | --- | --- | --- |
| POST | `/groups` | Admin | 200 `GroupDto { id, name, joinCode, leaderId?, members: [ { userId, role, joinedAtUtc } ] }` |
| GET | `/groups/{code}` | مسجل | 200 `GroupSummaryDto { id, name, leaderName?, memberCount }` |
| POST | `/groups/{code}/join` | Admin | 200 `GroupSummaryDto` |

---

## 6. SignalR

| البند | القيمة |
| --- | --- |
| المسارات | `wss://labaik-api.onrender.com/hubs/location` و`wss://labaik-api.onrender.com/hubs/events` |
| المصادقة | التوكن في الاستعلام: `?access_token=<accessToken>` (لا يمكن إرسال ترويسة في WebSocket). |
| النقل | WebSocket مباشرة بلا negotiate، كما يفعل التطبيق الآن. يجب أن تبقى WebSockets مفعلة على Render. |
| الاشتراك | عند الاتصال يضم الخادم الاتصال إلى كل مجموعة أنت عضو فيها، و`/hubs/location` يضمه أيضاً إلى كل دائرة عائلة أنت فيها. الاشتراك يُحسب **وقت الاتصال فقط**: بعد الانضمام لمجموعة أو دائرة، أو بعد تجديد التوكن، أعيدوا الاتصال. |
| موثوقية الدفع | الدفع الفوري أفضل جهد. البيانات محفوظة قبل الدفع، فعند إعادة الاتصال اجلبوها بـ REST. |

### `/hubs/location`

| الاتجاه | الاسم | الحمولة |
| --- | --- | --- |
| عميل إلى خادم | `UpdateLocation` | `(latitude: double, longitude: double, accuracyMeters: double?)`. نفس `POST /location`: يجب أن تكون المشاركة مفعلة، وإلا `HubException` برسالة تبدأ بـ `Location.SharingDisabled`. أخطاء الشكل تبدأ بـ `Validation:`. |
| خادم إلى عميل | `LocationUpdated` | `LiveLocationDto { userId, latitude, longitude, accuracyMeters?, updatedAtUtc }`. يصل لكل مجموعاتك، ولكل دائرة فعّل فيها المرسل المشاركة. |
| خادم إلى عميل | `StoppedSharing` | `{ userId }`. عند إيقاف المشاركة العامة (لكل المجموعات والدوائر)، أو إيقافها في دائرة، أو الخروج من دائرة. |

قد يصل نفس `LocationUpdated` مرتين إذا كان الشخص معك في مجموعة ودائرة معاً. أزيلوا التكرار بـ `userId` و`updatedAtUtc`.

### `/hubs/events` (استقبال فقط)

| الاتجاه | الاسم | الحمولة | من يستقبل |
| --- | --- | --- | --- |
| خادم إلى عميل | `MessageReceived` | `ChatMessageDto` (فيه `isRead = false` دائماً) | كل أعضاء المجموعة المتصلين، ومنهم المرسل. أزيلوا التكرار بـ `id`. |
| خادم إلى عميل | `NotificationReceived` | `NotificationDto` | المستلم فقط (يُعرف من `sub` في التوكن) |

لا توجد دوال من العميل إلى الخادم في `/hubs/events`. الرسائل تُرسل عبر REST.

---

## 7. ما يجب على Flutter فعله الآن

- [ ] احذفوا شاشة نوع الحساب واستدعاء `PUT /auth/account-type`. التسجيل العام معتمر فقط.
- [ ] اقرؤوا الأدوار من `user.roles` (أو `GET /auth/me`): `Company` تفتح واجهة الشركة، `Supervisor` أو `Admin` تفتح واجهة المشرف، والقائمة الفارغة معتمر. الحسابات المنشأة من الأعلى تدخل مباشرة بكلمة المرور المؤقتة بلا OTP.
- [ ] بعد أي تغيير دور أو 403 غير متوقع: `refresh-token` ثم أعيدوا الطلب مرة واحدة، ثم أعيدوا اتصال SignalR بالتوكن الجديد.
- [ ] أعيدوا اتصال `/hubs/location` و`/hubs/events` عند تغيّر المجموعة في `/home` أو بعد الانضمام لدائرة عائلة.
- [ ] عالجوا أنواع الإشعارات 8 إلى 12، واعرضوا الأنواع غير المعروفة بشكل عام بدل التعطل. افتحوا الشاشة المناسبة من `referenceId`: رسالة (8 و9)، بلاغ طوارئ (10، للمشرف عبر `GET /map/emergency/{id}`)، بلاغ طفل (11، عبر `GET /lost-child-reports/{id}`)، دائرة (12).
- [ ] الدردشة: أضيفوا رسائل الموقع (`kind = 2`)، وصورة المرسل، و`isRead`. استدعوا `POST /groups/{groupId}/messages/read` عند فتح الدردشة أو الوصول لآخرها، واعرضوا `home.group.unreadMessages` أو `messages/unread-count`.
- [ ] اعرضوا حالة "لا يوجد مشرف بعد" عندما تكون `leaderName` أو `supervisorId` أو `supervisorName` فارغة.
- [ ] تعاملوا مع `AnnouncementDto.groupId = null` (إعلان الشركة)، وفعّلوا شارة "إعلان مهم" (`importance = 2`)، و`unreadAnnouncements`، و`POST /announcements/{id}/read`.
- [ ] واجهة الشركة: اربطوا `/company/*`. أضيفوا المشرف عبر `POST /company/supervisors` **قبل** تعيينه لمجموعة. إضافة عضو ببريد جديد تنشئ حسابه، فلا تعتبروا ذلك خطأ.
- [ ] واجهة المشرف: نقطة التجمع، والحضور، وقائمة الأعضاء وبطاقة العضو، وبرنامج الرحلة، وقائمة المجموعات (`/supervisor/groups` مع `?groupId=` في الرئيسية).
- [ ] وضع الميدان: استبدلوا بيانات الأبواب الافتراضية بـ `GET /map/destinations`، واربطوا زر الطوارئ بـ `POST /map/emergency`.
- [ ] مركز المساعدة: `GET /pilgrim/supervisor` وبلاغ الطفل المفقود.
- [ ] دائرة العائلة: استبدلوا البيانات التجريبية على الجهاز بمسارات `/family-circles`.
- [ ] الرحلة الشخصية: يمكن الآن إرسال `pilgrimageType` في `PUT /journeys/{id}`.
- [ ] الأخطاء: اختاروا الرسالة المترجمة حسب `code` (وحسب `status` عندما لا يوجد `code`)، واربطوا أخطاء 422 بالحقول عبر مفاتيح `errors` بصيغة camelCase.

### حقول تبقى في Flutter (لا مكان لها في الخادم)

- ترتيب مدن المسار، واسم المسافر، واختيارات "لم أحدد بعد".
- `travelParty` اختيار متعدد في التطبيق: يُرسل `Group` إن وُجد، ثم `Family`، وإلا `Solo`.
- `isFirstTime` غير المجاب يُرسل `false`.
- موعد الانطلاق: التطبيق يسأل عن تاريخ واحد ويرسله كـ `arrivalAtUtc` لكل إقامة (قرار صاحب المشروع، والخادم ما زال يطلبه لكل إقامة). لا تُرسل الرحلة قبل وجود وسيلة الوصول ومكانه وإقامة واحدة على الأقل.

---

## 8. مؤجل أو معلّق

| البند | الحالة |
| --- | --- |
| الدردشة الفردية (معتمر مع مشرفه، عضو مع عضو) | مؤجلة بقرار صاحب المشروع. النوع 9 سيُعاد استخدامه لرسائل المشرف الفردية. |
| إشعارات FCM والتطبيق مغلق | مؤجلة. الإشعارات حالياً داخل التطبيق فقط (REST + `NotificationReceived`). |
| مراجعة المحتوى الديني | نصوص المناسك (`/rituals`) والأدلة (`/guides`) مؤقتة وتنتظر اعتماد جهة شرعية موثوقة قبل الإنتاج. |
| تسجيل الدخول بـ Google | الكود جاهز (`POST /auth/google`). ينتظر Client IDs لأندرويد وiOS والويب في إعدادات Render (`Authentication__Google__ClientIds__0..n`). بدونها يعيد 400. |
| بيانات خريطة الحرم | معلّمة `PendingReview`: أرقام الأبواب والإحداثيات تقريبية، وستُراجع مقابل خريطة رسمية. لا صور في الخادم بعد (`imageUrl = null`). |
| كلمة المرور المؤقتة | تُرسل نصاً في البريد ولا يُجبر المستخدم على تغييرها عند أول دخول. مهمة قبل الإنتاج. |
| رفع الملفات | لا توجد خدمة رفع. الصور والملفات روابط `https` فقط (`photoUrl` و`logoUrl` و`imageUrl` و`contractFileUrl`). |

### ملاحظات التشغيل

- **Render المجاني ينام** بعد نحو 15 دقيقة بلا طلبات. أول طلب بعدها يأخذ قرابة 50 ثانية، وهذا طبيعي. مهلة 60 ثانية في التطبيق مناسبة.
- **OpenAPI وScalar مفعّلان على النسخة المنشورة**: `https://labaik-api.onrender.com/openapi/v1.json` و`https://labaik-api.onrender.com/scalar/v1`. مسارات `/admin/*` و`/company/*` فيها أمثلة طلب واستجابة كاملة.
- البريد الفعلي يُرسل عبر Brevo فقط إذا كان `Email:Provider = Brevo` مع المفتاح على Render. غير ذلك يُكتب البريد (ومعه كلمة المرور المؤقتة أو رمز التحقق) في سجلات الخادم.
