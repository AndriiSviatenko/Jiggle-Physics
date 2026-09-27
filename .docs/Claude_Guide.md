# Jiggle Physics / Breast Physics / Secondary Motion у Unity — Research для реалізації production-системи

Дата дослідження: 2026-09-19. Цільова аудиторія: Unity-розробник та AI coding agent, який має з нуля реалізувати стабільну систему secondary motion (зокрема груди) для персонажів.

Позначки в тексті:
- **[SRC]** — твердження підтверджене джерелом (URL у тексті або в розділі 20).
- **[CODE]** — висновок зроблено з читання реального source code (файл/рядок вказано).
- **[ENG]** — інженерна рекомендація автора дослідження (не цитата джерела; перевіряти в проєкті).
- **[OLD]** — матеріал старий, але математика/алгоритм актуальні.

---

## 1. Executive summary

1. **Для грудей у production не використовуйте Rigidbody/Joint та Unity Cloth.** Стандарт індустрії — *procedural bone-based simulation*: для кожної jiggle-кістки симулюється одна (або кілька) точкових мас ("tail"/"particle") з пружиною до анімованої пози, потім результат конвертується в rotation (і опційно translation/scale) кістки у `LateUpdate`, **після** Animator. Так працюють Dynamic Bone, UniVRM SpringBone, UnityChan SpringBone, naelstrof JigglePhysics, EZSoftBone, Magica Cloth 2 BoneSpring **[CODE]**.
2. **Інтегратор:** Verlet (position-based, стійкий до "вибухів") або semi-implicit Euler зі *спрингом, параметризованим через frequency (Hz) + damping ratio ζ*. Критично: **фіксований внутрішній timestep (substeps) + clamp кількості кроків + interpolation**, бо майже всі tutorial-реалізації (і частина популярних бібліотек) frame-rate dependent **[CODE]**.
3. **Три речі, без яких система не production-ready:** (а) коректна взаємодія з Animator (читання анімованої пози кожен кадр і скидання до rest-пози для неанімованих кісток), (б) обмеження (max angle / max displacement / length constraint / speed limit), (в) reset/teleport handling.
4. **Inertia control (world vs local)** — головний "художній" параметр для грудей: скільки руху персонажа у світі (біг, стрибок, ліфт, машина) передається у jiggle. Реалізується симуляцією в просторі якоря (chest/hips/root) з частковим "переносом" історії Verlet на дельту якоря (UniVRM `center`, naelstrof `ignoreRootMotion`, Magica `World Inertia`) **[CODE][SRC]**.
5. **Collision** — достатньо математичних proxy-колайдерів (sphere / capsule / plane), без PhysX. Для грудей: capsule на upper arm, plane/sphere для грудної клітки, sphere-sphere між лівою/правою. Unity Colliders через PhysX для цього — дорожче й гірше контрольовано.
6. **Performance:** 1 персонаж — будь-яка нормальна реалізація на MonoBehaviour без алокацій. 10+ — один менеджер (без `LateUpdate` на кожну кістку). 50+ — Burst + Jobs + `TransformAccessArray`, LOD і distance/visibility culling. 100+ — плюс зниження частоти симуляції для далеких персонажів і "sleep".
7. **Multiplayer:** jiggle — **чисто косметична локальна симуляція** на кожному клієнті. Не синхронізувати кістки. Синхронізувати лише те, що вже синхронізується (transform персонажа, анімаційний стан) + за потреби дискретні параметри (профіль/outfit). Головне — щоб proxy-персонажі отримували *інтерпольовані* (гладкі) трансформи, інакше jiggle реагує на ривки tick-rate.
8. **Buy vs build:** якщо немає вимоги писати своє — Magica Cloth 2 (BoneSpring з пресетом "Breast", teleport detection, Burst) або naelstrof JigglePhysics (MIT, Burst/Jobs, source доступний) — найсильніші варіанти станом на 2026-09.

---

## 2. Як працює Jiggle Physics (концептуально)

Secondary motion = реакція "м'якої" частини на рух "жорсткої" частини, до якої вона прикріплена. Груди прикріплені до грудної клітки (chest/upper chest bone). Коли грудна клітка прискорюється/обертається, м'яка тканина через інерцію відстає, пружно повертається до рівноваги і затухає.

Мінімальна модель на кістку:
```
anchor (parent bone, анімований)  ──► rest/target point = де "хвіст" кістки був би без фізики
                                     simulated point   = де "хвіст" є зараз (маса з інерцією)
spring:  тягне simulated → target
damping: гасить швидкість
gravity/external: зсуває рівновагу
constraints: довжина, кут, max displacement, collisions
output: rotation кістки = FromTo(animatedDir, simulatedDir) * animatedRotation
```
Саме так реалізовано у UniVRM (`nextTail` → `FromToRotation(rotation*boneAxis, nextTail - head)`) **[CODE: UniGLTF/Runtime/SpringBoneJobs/UpdateFastSpringBoneJob.cs]** і в naelstrof (`ApplyPose`: `FromToRotationFromNormalizedVectors(cachedAnimatedVector, simulatedVector)` * input rotation, з `blend`) **[CODE: JiggleJobSimulate.cs]**.

Для грудей часто корисно, крім rotation, дати **обмежену translation** (bounce вгору/вниз) і **squash/stretch** (scale вздовж осі). Rotation-only виглядає як "маятник", translation додає відчуття маси.

Реалістичні амплітуди (орієнтир для "realistic"-пресету): дослідження Scurr et al. (J. Sports Sciences, 2011, D-cup, без підтримки) — сумарне 3D-переміщення соска відносно тулуба ~4.2 ± 1.0 см при ходьбі і ~15.2 ± 4.2 см при бігу; вертикальна компонента ~50% при бігу; підтримка (бра) зменшує амплітуду, але не напрям **[SRC: https://pubmed.ncbi.nlm.nih.gov/21077006/]**. Для одягненого ігрового персонажа реально використовують суттєво меншу амплітуду.

---

## 3. Математика

### 3.1 Hooke's law та damped harmonic oscillator
Сила пружини: `F = -k (x - x_rest)`. Демпфер: `F_d = -c v`. Рівняння руху:
```
m x'' = -k (x - x_rest) - c x' + F_ext
```
Перепараметризація, якою варто користуватись (artist-friendly, незалежна від маси):
- natural angular frequency `ω = sqrt(k/m)`, частота `f = ω / 2π` (Гц — "скільки коливань на секунду");
- damping ratio `ζ = c / (2 sqrt(k m))`.
```
x'' = -ω² (x - x_rest) - 2ζω x' + a_ext
```
- `ζ < 1` — underdamped (коливається, "jiggle"); `ζ = 1` — critically damped (найшвидше повернення без overshoot); `ζ > 1` — overdamped ("в'язко", повільно).
- Для грудей: `ζ ≈ 0.2–0.6`, `f ≈ 1.5–4 Hz` **[ENG]**.
- Маса у такій параметризації не потрібна — вона "захована" в ω. `mass` як окремий параметр має сенс лише для взаємодії з зовнішніми силами/колізіями.

Джерела з нормальним поясненням: Allen Chou "Numeric Springing" (частота + ζ, implicit Euler, precise control через half-life) **[SRC: https://allenchou.net/2015/04/game-math-precise-control-over-numeric-springing/]**; Daniel Holden "Spring-It-On: The Game Developer's Spring-Roll-Call" (точні аналітичні розв'язки, half-life, critically damped, implicit springs) **[SRC: https://theorangeduck.com/page/spring-roll-call]**; Ryan Juckett "Damped Springs" (аналітичний розв'язок для всіх трьох режимів) **[SRC][OLD]: https://www.ryanjuckett.com/damped-springs/**; t3ssel8r "Giving Personality to Procedural Animations using Math" — second-order system з параметрами f, ζ, r (response/anticipation) і аналіз стабільності **[SRC: https://www.youtube.com/watch?v=KPoeNZZ6H4s]**.

### 3.2 Параметр "response" (t3ssel8r)
Second-order system `y + k1 y' + k2 y'' = x + k3 x'`, де `k1 = ζ/(πf)`, `k2 = 1/(2πf)²`, `k3 = r ζ /(2πf)`. `r` керує початковою реакцією: `r=0` — повільний старт, `r=1` — миттєвий, `r>1` — overshoot, `r<0` — anticipation. Для jiggle корисно як "snappiness" **[SRC: t3ssel8r video; код-транскрипт: https://github.com/SalvatoreScalia/Giving-Personality-to-Procedural-Animations-using-Math]**.

### 3.3 Інтегратори
| Метод | Формула (per step h) | Стабільність | Коментар |
|---|---|---|---|
| Explicit Euler | `x += v h; v += a(x) h` | нестабільний для пружин (енергія росте) | не використовувати |
| Semi-implicit (symplectic) Euler | `v += a(x,v) h; x += v h` | стабільний при `ω h < 2` (без демпфування); з великим ζ і великим h — може вибухнути | стандарт для ігор; Chou попереджає, що може "blow up" при деяких конфігураціях **[SRC]** |
| Implicit Euler (закрита форма для лінійного спрингу) | див. нижче | безумовно стабільний, але штучно гасить енергію | Chou: "always stable" **[SRC]** |
| Аналітичний (exact) | Holden/Juckett | точний для будь-якого h | лише для лінійного спрингу без обмежень/колізій |
| Position Verlet | `x_new = x + (x - x_prev)*(1-drag) + a h²` | дуже стійкий, швидкість неявна → constraints (проєкція позицій) автоматично коригують швидкість | стандарт для spring bones (Jakobsen 2001; VRM; naelstrof; UnityChan) **[SRC][CODE]** |
| PBD / XPBD | predict → project constraints → v=(x-x_prev)/h | безумовно стійкий; XPBD робить жорсткість незалежною від h та кількості ітерацій | Macklin/Müller/Chentanez 2016 **[SRC]** |

Implicit Euler для спрингу (Chou, `NumericSpring.cs`):
```
f      = 1 + 2 h ζ ω
oo     = ω²
hoo    = h * oo
hhoo   = h * hoo
detInv = 1 / (f + hhoo)
x_new  = (f*x + h*v + hhoo*x_target) * detInv
v_new  = (v + hoo*(x_target - x)) * detInv
```
**[SRC: https://github.com/TheAllenChou/unity-cj-lib/blob/master/Unity%20CJ%20Lib/Assets/CjLib/Script/Math/NumericSpring.cs]**

Critically damped exact (Holden):
```
y    = (4 ln2 / halflife) / 2
j0   = x - goal
j1   = v + j0*y
e    = exp(-y*h)
x    = e*(j0 + j1*h) + goal
v    = e*(v - j1*y*h)
```
**[SRC: https://theorangeduck.com/page/spring-roll-call]**

### 3.4 Timestep, frame-rate independence, substeps
Ключова проблема: будь-який вираз виду `v *= (1 - damping)` або `x = Lerp(x, target, k)` **без dt** залежить від FPS. Навіть `Lerp(x, t, k*dt)` — лише наближено незалежний.
- Правильна frame-independent експонента: `x = Lerp(x, t, 1 - exp(-λ dt))`; для "per-60fps-frame" коефіцієнта `d`: `factor = pow(1 - d, dt * 60)`.
- Найнадійніше: **fixed internal step** (напр. 1/60 або 1/90 с) + accumulator + interpolation між двома останніми станами (Gaffer "Fix Your Timestep") **[SRC: https://gafferongames.com/post/fix_your_timestep/]**.
- **Cap** на кількість кроків за кадр (2–4) і відкидання "боргу" часу — захист від "spiral of death" та вибуху після паузи/hitch.
- Verlet зі змінним dt потребує time-corrected Verlet: `x_new = x + (x - x_prev)*(h/h_prev) + a h²` — простіше взагалі не мати змінного h.

Як це робить naelstrof JigglePhysics **[CODE: JigglePhysics.cs `ScheduleSimulate`, JiggleJobInterpolation.cs]**: симуляція тільки фіксованими кроками `Time.fixedDeltaTime` (але запускається з `LateUpdate`, не з `FixedUpdate`); якщо відстали на кілька кроків — виконується **максимум один** крок за кадр, решта часу відкидається (`skips = Mathf.Min(skips + 1, 1)`); рендер-поза — lerp між двома останніми симульованими станами з затримкою (`timeCorrection = fixedDeltaTime*2`), а коренева позиція "підтягується" до реальної (`snapToReal`), щоб персонаж не відставав від своїх грудей.

Як це робить UnityChanSpringBone **[CODE: Runtime/SpringManager.cs]**: `timeStep = 1/simulationFrameRate` (дефолт 60) — **фіксований крок, один раз на кадр, незалежно від реального часу**. Результат: при 30 FPS симуляція йде вдвічі повільніше (slow-motion), при 144 FPS — у 2.4 рази швидше. Це приклад того, як *не треба* робити "fixed step".

UniVRM **[CODE: UpdateFastSpringBoneJob.cs]**: `nextTail = current + (current - prev)*(1 - drag) + stiffnessDir*stiffness*dt + external*dt` — drag застосовується *per frame*, stiffness/gravity множаться на `dt` (а не на `dt²`) — отже поведінка залежить від FPS. У специфікації VRMC_springBone немає вимог щодо фіксованого timestep **[SRC: vrm-specification README]**.

EZSoftBone **[CODE: Runtime/EZSoftBone.cs:592–668]**: `speed *= 1 - damping` та `Lerp(pos, expected, stiffness/iterations)` — per-frame коефіцієнти, тобто frame-rate dependent; є `DeltaTimeMode.Constant` як обхідний шлях.

Unity Animation Rigging "Jiggle Chain" (GDC 2019 sample): користувачі фіксують, що при високому FPS хвіст "майже перестає трястися" — відкритий тред станом на 2025-09 **[SRC: https://discussions.unity.com/t/how-to-make-framerate-independent-rigging-constraint/1685301]**.

### 3.5 Local-space vs world-space; inertia
- **Pure world-space** симуляція: реалістично (ліфт/машина/біг передаються), але: teleport → вибух, швидкий рух → перерозтяг, рух камери не впливає (добре), платформи/транспорт дають небажаний jiggle.
- **Pure local-space** (у просторі chest): стабільно, але втрачається реакція на лінійні прискорення персонажа — груди реагують тільки на анімацію.
- **Production: гібрид.** Симулюємо у world-space, але перед інтеграцією зсуваємо *історію* (prev і current) на частину руху якоря:
  `prev += anchorDelta * (1 - worldInertia); cur += anchorDelta * (1 - worldInertia)`.
  Це в точності naelstrof `ignoreRootMotion` **[CODE: VerletIntegrate]** і концептуально UniVRM `center` ("inertia is evaluated in the center space… gravity in world space") **[SRC: VRMC_springBone-1.0 README]**, Magica Cloth 2 "World Inertia / Local Inertia / Movement Speed Limit / Rotation Speed Limit" **[SRC: https://magicasoft.jp/en/mc2_magicacloth_inertia/]**.
- Додатково — **clamp швидкості якоря** (movement/rotation speed limit): при швидкості персонажа вище ліміту надлишок переноситься в історію, а не в jiggle.
- naelstrof розділяє `drag` (гасить локальну відносно батька швидкість) та `airDrag` (гасить world-швидкість) — завдяки цьому "elevators and vehicles work as expected" **[SRC: README; CODE: VerletIntegrate]**.

### 3.6 Gravity
- Gravity зсуває точку рівноваги ("sag"). Якщо модель вже змодельована з врахуванням гравітації (rest pose — "обвислі"), то додавання повної gravity дасть подвійне провисання. Практика: `gravity` малий (0–0.3 від Physics.gravity), або компенсувати: `target += -g * gravityFactor / ω²` (статичне провисання = g/ω²) **[ENG]**.
- Корисно: gravity в world-space (коли персонаж лежить — груди падають в сторону), це і є головна причина мати gravity взагалі.

### 3.7 Constraints
- **Length constraint** (bone length): після інтеграції `tail = head + normalize(tail-head) * length` — робить ротаційний jiggle **[CODE: VRM, UnityChan]**. Для "stretchy" jiggle length constraint робиться м'яким (`lengthElasticity` у naelstrof — lerp до довжини).
- **Angle constraint / max angle**: обмежити кут між animatedDir і simulatedDir (конус). naelstrof має `angleLimit` + `angleLimitSoften` **[CODE]**, UniVRM має `Anglelimit` (Cone/Hinge/Spherical) **[CODE: UniGLTF/Runtime/SpringBoneJobs/Anglelimit/*]**.
- **Max displacement**: clamp `|tail - target| ≤ maxDisp` (жорсткий або soft: `d' = maxDisp * tanh(d/maxDisp)`) **[ENG]**.
- **Velocity clamp**: `|x - x_prev| ≤ vMax*h` — найдешевший запобіжник від вибухів.
- **NaN guard**: UnityChanSpringBone при NaN скидає tail у rest **[CODE: SpringBone.cs:165–192]** — обов'язково мати аналог.

### 3.8 Collision response (position-based)
Для Verlet/PBD: знайти penetration depth `p = (r_particle + r_collider) - distance`, якщо `p>0` — посунути particle вздовж нормалі на `p` (проєкція). Швидкість виправляється автоматично (Verlet), friction — масштабувати тангенційну складову `(x - x_prev)` **[SRC: Jakobsen 2001]**. naelstrof робить це для sphere/capsule/plane з перевіркою і точки, і сегмента кістки **[CODE: DoDepenetration]**.

### 3.9 Що з цього реально потрібно для хорошого jiggle
Обов'язково: spring+damping у формі (f, ζ) або Verlet із коефіцієнтами на фіксований крок; fixed substeps + cap + interpolation (або хоча б правильне dt-масштабування); local/world inertia mix; max angle/max displacement; velocity clamp; NaN guard; reset. Бажано: gravity world-space; sphere/capsule collisions; squash/stretch. Не потрібно: точна маса, rigid body inertia tensor, повний soft-body FEM, XPBD (якщо немає ланцюгів/тканини).

---

## 4. Підходи (карта технологій)

### 4.1 Rigidbody + Joints (SpringJoint / ConfigurableJoint / CharacterJoint)
- **Принцип:** кістка = Rigidbody, прикріплений joint'ом до kinematic Rigidbody на chest. PhysX рахує в `FixedUpdate`.
- **Проблеми:**
  - Animator рухає chest (kinematic) через transform → PhysX не знає швидкості; Unity прямо радить: "Never use direct transform access with kinematic bodies jointed to other bodies" — пропускаються внутрішні velocity-обчислення **[SRC: https://docs.unity3d.com/Manual/RagdollStability.html]**. Потрібен `MovePosition/MoveRotation` + `Animator.updateMode = Fixed` (раніше AnimatePhysics) — що змінює таймінг всієї анімації.
  - Ратіо мас >10 — jitter **[SRC: там само]**; треба `projectionMode`, 10–20 solver iterations.
  - Частота FixedUpdate (50 Гц за замовч.) < FPS → потрібна Rigidbody interpolation **[SRC: https://docs.unity3d.com/Manual/rigidbody-interpolation.html]**, а інтерпольований transform кістки і анімований skeleton все одно розсинхронізуються на кадр.
  - Rigidbody в ієрархії skinned mesh, яка рухається Animator'ом → ціна sync transforms, а `Physics.autoSyncTransforms` краще тримати вимкненим **[SRC: https://docs.unity3d.com/ScriptReference/Physics.SyncTransforms.html]**.
  - Teleport: треба вручну телепортувати всі Rigidbody + скидати швидкості.
- **SpringJoint** — лише позиційна пружина між двома тілами, без контролю кута; **CharacterJoint** — для ragdoll (twist/swing limits), для jiggle незручний; **ConfigurableJoint** — найгнучкіший (drives з spring/damper, лінійні/кутові ліміти), єдиний із трьох, з якого можна зібрати щось придатне.
- **Коли доречно:** персонаж у ragdoll (тоді груди можна приєднати до ragdoll-тіла ConfigurableJoint'ом з drives), або гра вже повністю фізична (active ragdoll). Для звичайного анімованого персонажа — **ні**.

### 4.2 Procedural spring simulation (damped oscillator per bone)
- Позиційний спринг для tail-точки або ротаційний спринг (кут/кватерніон) у `LateUpdate`.
- **Позиційний спринг** (point mass) → конвертація у rotation через FromTo — простий, передбачуваний, легко обмежувати.
- **Ротаційний спринг** (angular velocity, кватерніони): `ω_vec += (-k * axisAngle(q_err) - c ω_vec) h; q = exp(ω_vec h) * q`. Точніше для "повороту навколо pivot", але складніше з обмеженнями, twist'ом і колізіями. Holden описує quaternion springs у Spring-It-On **[SRC]**.
- Для грудей зазвичай: **позиційний спринг tail'а + обмежена translation кістки** **[ENG]**.

### 4.3 Verlet spring bones (Dynamic Bone / VRM / UnityChan / naelstrof)
- Particle на кожен сегмент, Verlet + stiffness (тяга до анімованої пози) + drag + gravity, потім constraints (length, angle), collisions, запис rotation. Найбільш перевірений підхід для hair/tails/breasts.
- Для одиночної кістки (типові груди) — додають "віртуальну" кінцеву точку (`endLength`/`End Offset` у Dynamic Bone; у naelstrof — virtual end particles `parent.pose*2 - parent.parentPose` **[CODE: Cache]**).

### 4.4 PBD / XPBD
- Для ланцюгів/тканини/volume-preserving soft body. XPBD дає жорсткість, незалежну від timestep та кількості ітерацій **[SRC: Macklin et al. 2016]**. Ten Minute Physics — найкраще практичне введення (епізоди 09 XPBD, 10 soft bodies) **[SRC]**.
- Для 1–2 кісток на груди — надлишково. Має сенс, якщо: груди + одяг + волосся симулюються однією системою (як Magica Cloth 2), або потрібна tetrahedral soft body.

### 4.5 Animation Rigging
- Constraints виконуються як `IAnimationJob` *всередині* Animator graph після оцінки кліпів **[SRC: Unite Copenhagen 2019 "Extending the Animation Rigging package with C#"]** — тобто це "правильне" місце в pipeline (IK і jiggle разом, без боротьби з Animator).
- `DampedTransform` — затримка позиції/ротації (0..1) відносно source **[SRC: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/DampedTransform.html]**. Це не пружина (немає overshoot), дає "лаг", а не "jiggle". Plus — frame-dependency питання (див. 3.4).
- Можна написати власний constraint (`RigConstraint<TJob,TData,TBinder>`, `IWeightedAnimationJob`, `ProcessAnimation(AnimationStream)`); складність — стан (velocity/prev) треба тримати в NativeArray, dt брати з `stream.deltaTime`.
- **Коли:** проєкт вже живе на Animation Rigging, потрібна пряма інтеграція з IK (напр. рука торкається грудей). Інакше — простіше LateUpdate-солвер.

### 4.6 Blendshape-based secondary motion
- Симульований скаляр/вектор (спринг) керує вагами blendshapes (напр. `Breast_Up`, `Breast_Down`, `Breast_Squash`). Дуже дешево, повний художній контроль форми (об'єм зберігається, бо форму задає артист), не потрібні кістки.
- Мінуси: blendshapes дорогі на GPU/CPU skinning на мобільних при багатьох вагах, лінійна інтерполяція вершин (нема обертання), не взаємодіє з колізіями.
- **Гібрид**: кістка для swing + blendshape для squash/stretch/corrective — топ-якість **[ENG]**.

### 4.7 Cloth (Unity Cloth, Magica, Obi)
- Unity `Cloth`: лише sphere/capsule/"conical capsule" колайдери, до ~16 колайдерів, односторонній вплив, колайдери треба явно додавати **[SRC: https://docs.unity3d.com/2021.3/Documentation/Manual/class-Cloth.html; discussions]**. Не має volume → груди "здуваються". **Не рекомендовано** для тіла.
- Magica Cloth 2 MeshCloth/BoneCloth — PBD-подібний солвер на Burst, добре для одягу; для грудей у них є окремий BoneSpring.

### 4.8 Soft body (Obi Softbody, custom FEM/shape matching)
- Obi Softbody ($55, Virtual Method) — particle-based, shape matching, collisions, self-collisions **[SRC: https://assetstore.unity.com/packages/tools/physics/obi-softbody-130029]**. Дорожче, складніше в налаштуванні, мобільні — тільки для 1–2 об'єктів. Для fighting/hero-close-up — можливо; для масової гри — ні.

### 4.9 Hybrid animation + physics
- Анімація містить базову (baked) secondary motion, фізика додає лише реакцію на непередбачуване. Або weight-blend: `finalRot = Slerp(animated, simulated, physicsWeight)` де вага зменшується в катсценах (naelstrof `blend` **[CODE]**, UnityChan `dynamicRatio` для анімованих кісток **[CODE: SpringManager.cs:90–123]**).

### 4.10 Commercial/runtime solutions — див. розділ 14.

---

## 5. Порівняльна таблиця

Оцінки 1–5 (5 = найкраще) — **[ENG]** на основі коду/доків вище.

| Підхід | Складність | Стабільн. | Контроль художника | CPU | GC | Low FPS | High FPS | Determ. | Animator | Humanoid | Mobile | PC | MP | Production |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Rigidbody+ConfigurableJoint | 3 | 2 | 2 | 2 (PhysX) | 5 | 3 (fixed step) | 3 (потрібна interp.) | ~ (PhysX не гарантує крос-платформ.) | конфлікт (kinematic driving) | 3 | 2 | 3 | local | 2 |
| SpringJoint | 2 | 2 | 1 | 2 | 5 | 3 | 3 | ~ | конфлікт | 2 | 2 | 2 | local | 1 |
| CharacterJoint | 3 | 3 | 2 | 2 | 5 | 3 | 3 | ~ | тільки ragdoll | ragdoll | 2 | 3 | local | 2 (ragdoll) |
| Procedural spring (per-frame dt, naive) | 1 | 2 | 3 | 5 | 5 | 1 | 2 | ні | LateUpdate OK | 4 | 5 | 5 | local | 2 |
| Procedural spring (f, ζ, fixed substeps) | 2 | 5 | 5 | 5 | 5 | 4 | 5 | так (при фікс. кроці) | LateUpdate OK | 5 | 5 | 5 | local | 5 |
| Verlet spring bones | 2–3 | 4–5 | 4 | 5 | 5 | 3–4 | 4 | так (фікс. крок) | LateUpdate OK | 5 | 5 | 5 | local | 5 |
| XPBD chains | 4 | 5 | 3 | 4 | 5 | 5 | 5 | так | LateUpdate OK | 4 | 4 | 5 | local | 4 (складні ланцюги) |
| Animation Rigging DampedTransform | 1 | 4 | 2 (лише лаг) | 4 | 5 | 3 | 2 | ні | вбудовано | 4 | 4 | 4 | local | 3 |
| Custom AR constraint (IAnimationJob) | 4 | 4 | 4 | 4 | 5 | залежить | залежить | так | вбудовано | 5 | 4 | 5 | local | 4 |
| Blendshape-driven | 2 | 5 | 5 | 4 (GPU/skin cost) | 5 | 5 | 5 | так | незалежно | 5 | 3 | 5 | local | 4 (як доповнення) |
| Unity Cloth | 2 | 3 | 2 | 2 | 5 | 3 | 3 | ні | окремо | 2 | 1 | 3 | local | 1 (для тіла) |
| Soft body (Obi) | 4 | 4 | 3 | 1 | 4 | 3 | 4 | ні | інтеграція | 3 | 1 | 3 | local | 2 |
| Magica Cloth 2 BoneSpring | 1 | 5 | 4 | 5 (Burst) | 5 | 4 | 5 | — | LateUpdate / Unity Physics mode | 5 | 5 | 5 | local | 5 |

---

## 6. Unity animation/physics pipeline

### 6.1 Порядок кадру (спрощено)
```
[FixedUpdate × N] → [internal physics step × N] → OnTrigger/OnCollision
Update
[Internal animation update]: state machine → ProcessGraph (кліпи + Animation Rigging jobs) → OnAnimatorMove (root motion) → OnAnimatorIK → WriteProperties (запис у Transforms)
LateUpdate            ← сюди jiggle
(rendering: skinning використовує фінальні Transforms)
```
**[SRC: https://docs.unity3d.com/6000.0/Documentation/Manual/execution-order.html]**. `FixedUpdate` може викликатися 0..N разів за кадр.

`AnimatorUpdateMode`: `Normal` (Update), `Fixed` (FixedUpdate; "evaluate animation independent of frame rate"; у старих версіях — `AnimatePhysics`), `UnscaledTime` (ігнорує `Time.timeScale`) **[SRC: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimatorUpdateMode.html]**.

### 6.2 Правильний pipeline
```
Animator (Normal) пише анімовану позу
  → [Animation Rigging jobs: IK, aim — всередині Animator]
  → LateUpdate (JiggleSystem, execution order після всіх, хто рухає персонажа в LateUpdate: камера-рига не рахується)
      1. Capture: зчитати анімовану позу jiggle-кісток і якорів (або відновити rest для неанімованих)
      2. Simulate: fixed substeps (Burst job)
      3. Constraints: length / angle / max displacement / velocity
      4. Collision: proxy colliders (їх позиції теж зчитати після Animator)
      5. Write: rotation/position/scale кісток
  → Rendering / skinning
```
Використовуйте `[DefaultExecutionOrder(10000)]` на менеджері jiggle (або Script Execution Order) — щоб він був після character controller'а, який може рухати root у `LateUpdate`, і після інших процедурних скриптів (look-at, foot IK у LateUpdate) **[ENG]**. Камера, що слідує за персонажем, має бути *після* jiggle, якщо вона дивиться на кістки (рідко).

### 6.3 "Animator перезаписує фізику" — механіка і фікси
- Animator пише в Transform тільки ті властивості, які мають криві хоча б в одному кліпі контролера (binding). Якщо jiggle-кістка **анімована** — Animator кожен кадр перезаписує її (це добре: ми отримуємо свіжу target-позу, а в LateUpdate перекриваємо). Якщо **не анімована** — Animator її не чіпає, і на наступний кадр у Transform лежить *наш власний результат з минулого кадру* → система сприймає його як "анімовану позу" → feedback loop, дрейф, "груди повільно відпливають".
- **Фікс А (EZSoftBone):** у `Update()` повертати кістки в rest local pose (`RevertTransforms`), у `LateUpdate()` — симулювати **[CODE: EZSoftBone.cs:353–375]**. Анімовані кістки Animator все одно перепише між Update та LateUpdate.
- **Фікс Б (naelstrof, надійніший):** запам'ятати local pose, яку ми записали; у наступному кадрі перед симуляцією порівняти: якщо transform не змінився — його ніхто не анімував → відновити rest; якщо змінився position/rotation — це нова анімована поза, прийняти як rest **[CODE: JiggleJobBulkTransformReset.cs, `GetChangedFlags`]**. Працює і з Animator, і з Timeline, і з кастомними скриптами, і з вимкненим Animator.
- **Write Defaults / Humanoid:** на humanoid-рігах non-humanoid кістки (груди) анімуються як generic transforms, лише якщо кліпи їх містять і маска імпорту їх пропускає. Не покладайтесь на "Animator скидає кістку" — використовуйте фікс Б.
- **Animator.cullingMode = CullUpdateTransforms/CullCompletely**: коли персонаж offscreen, Animator не пише пози → вимикайте jiggle разом з ним, а при поверненні — reset (див. edge cases).

### 6.4 FixedUpdate vs LateUpdate для jiggle
- Симулювати в `FixedUpdate` — погано: Animator (Normal) оновлюється в Update, тож у FixedUpdate ви бачите позу з минулого кадру; кількість FixedUpdate за кадр 0..N → jitter; запис у кістки з FixedUpdate буде перезаписаний Animator'ом.
- **Правильно:** запускати *власний* fixed-step акумулятор усередині `LateUpdate` (як naelstrof) **[CODE]**. Єдиний виняток — якщо персонаж рухається Rigidbody без interpolation в FixedUpdate: тоді якір "стрибає" з частотою фізики → jiggle тремтить. Фікс — Rigidbody interpolation на персонажі (або режим "Unity Physics" як у Magica BoneSpring, що оновлюється синхронно з фізикою "to suppress vibration when character movement occurs within FixedUpdate") **[SRC: https://magicasoft.jp/en/magica-cloth-bone-spring-2/]**.

### 6.5 Root motion
- Root motion застосовується в `OnAnimatorMove`/автоматично до кореня; для jiggle це просто рух якоря. Проблема: root motion дає "ривки" на переходах state → імпульс у jiggle. Фікс: velocity clamp + speed limit якоря + world inertia < 1.

### 6.6 Physics step змінюється
`Time.fixedDeltaTime` може змінюватись (slow-mo). Якщо ваш солвер використовує *власну* константу (напр. `simulationRate = 90 Hz`), він не залежить від налаштувань фізики. naelstrof прив'язаний до `Time.fixedDeltaTime` (`SetFixedDeltaTime`) **[CODE]** — зміна fixedDeltaTime змінює частоту jiggle-симуляції; коефіцієнти drag задані "на крок", отже поведінка зміниться. Рекомендація: власна константа кроку в налаштуваннях системи **[ENG]**.

---

## 7. Rigging

### 7.1 Які кістки потрібні
Мінімум: `Breast_L`, `Breast_R`, дочірні до `Chest`/`UpperChest` (не до `Spine`, бо тоді дихання/нахил верхньої частини тіла будуть неправильно впливати).
Варіанти:
1. **Одна кістка на сторону (swing):** pivot біля основи (на грудній клітці, приблизно в центрі основи молочної залози, трохи всередині тіла), вісь — вперед/трохи вниз по напрямку "до соска". Найпоширеніше, дешево. Потрібен end offset (віртуальний tail) = довжина до соска.
2. **Дві кістки (root + tip / chain):** `Breast_L` (swing) → `Breast_L_Tip` (translation/scale). Дає "хвилю" і squash-stretch; tip-кістку симулювати з меншою жорсткістю.
3. **Root + offset bone для translation:** `Breast_L_Root` (статична, в rest) → `Breast_L` (симулюється translation+rotation). Translation не ламає ієрархію дочірніх кісток.
4. **Нижня/верхня кістки** (under-breast helper) — для корекції складки під грудьми (corrective, часто driven, а не simulated).

VRChat/Magica/naelstrof — всі вимагають, щоб jiggle-корінь мав батька-якоря і (для одиночної кістки) або дочірню кістку, або end offset **[CODE: naelstrof Cache "singular isolated root bone" edge case]**.

### 7.2 Pivot placement та орієнтація
- Pivot занадто близько до шкіри → при обертанні шкіра на основі "відривається"/проходить крізь тіло. Занадто глибоко → рух надто "лінійний". Орієнтир: 30–50% глибини від поверхні шкіри до ребер **[ENG]**.
- **Local axes:** зробіть bone roll симетричним (Blender: *Symmetrize*, *Recalculate Roll*). Одна з осей кістки (у Blender — Y вздовж кістки) має дивитись у бік соска. Солвер повинен визначати "bone axis" з даних (напрямок до child/end offset), а не хардкодити `Vector3.forward` — тоді mirrored rig працює автоматично (UniVRM зберігає `boneAxis` та `initialLocalRotation` на старті **[CODE]**).
- **Scale:** у Blender застосувати scale (Ctrl+A → All Transforms) до armature і mesh; у FBX export: *Apply Scalings = FBX All* або "Apply Unit", `Forward -Z / Up Y`, вимкнути *Add Leaf Bones* (зайві кістки заважають) **[ENG — стандартна Blender→Unity практика]**. Uniform scale 1 на всіх кістках у rest.

### 7.3 Weight painting / skinning
- Плавний градієнт: 1.0 у центрі/соску → 0 на межі з грудною кліткою, з широким фалоффом вгору (до ключиці частково) і різким — під грудьми (складка).
- Не давати вагу jiggle-кістки вершинам руки/пахви, інакше колізія з рукою "тягне" руку.
- Max 4 bones/vertex (Unity Quality → Skin Weights, мобільні часто 2) — перевіряйте, що обрізання ваг не з'їдає jiggle-вплив.
- Тестові пози: T-pose, руки донизу, руки вгору, нахил вперед (gravity), лежачи на спині.
- Blender-інструменти для превью: naelstrof **Jiggle Physics** (fork Wiggle 2, Verlet, Blender 4.2+, Blender Extensions) **[SRC: https://extensions.blender.org/add-ons/jiggle-physics/]**; artellblender **Spring Bones** (автор Auto-Rig Pro; самі автори кажуть "not production ready") **[SRC: https://github.com/artellblender/springbones]**.

### 7.4 Humanoid vs Generic
- Humanoid: груди — не humanoid-кістки; вони лишаються як звичайні transforms. Retargeting humanoid-анімації **не** переносить їхні криві між моделями. Отже на humanoid-рігах груди або неанімовані (частіше), або анімуються кліпами саме цієї моделі.
- Generic: криві застосовуються як є.
- Для обох: система має працювати з неанімованими кістками (див. 6.3 фікс Б).

### 7.5 Симетрія
- Одна `JiggleChainDefinition` + прапорець mirror або автопідбір пари за іменем (`_L/_R`, `.L/.R`, `Left/Right`) **[ENG]**.
- Параметри — з одного профілю; для "живості" — невеликий random offset частоти (±3–5%) між сторонами, інакше рух ідеально синхронний і виглядає механічно **[ENG]**.
- Mirrored rig з negative scale (-1 по X) — поширена помилка експорту; див. edge cases.

---

## 8. Collisions

### 8.1 Що з чим стикається
| Пара | Потреба | Proxy |
|---|---|---|
| Груди ↔ грудна клітка/torso | не провалюватись "всередину" при сильному відхиленні назад | Plane (normal = chest forward) або велика sphere всередині торсу ("keep out") |
| Груди ↔ upper arm / forearm | руки притиснуті, обійми, схрещені руки | Capsule на кожній кістці руки |
| Груди L ↔ R | не проходять одна крізь одну при ударі збоку | sphere-sphere між tail-точками (обидві рухаються, ділимо корекцію 50/50) |
| Груди ↔ одяг | одяг зазвичай skinned на ті ж кістки → колізія не потрібна; щільний одяг = інший профіль (менша амплітуда) | параметри, не колізія |
| Груди ↔ environment | підлога/стіна при лежанні | plane або кілька scene-колайдерів (рідко потрібно) |
| Self-collision | лише для soft body / cloth | не потрібно для bone jiggle |

### 8.2 Порівняння методів
- **Unity Colliders (PhysX) + Rigidbody** — колізії з усім світом, але: jiggle має жити у FixedUpdate, дорогі sync transforms, мало контролю. Для jiggle — ні.
- **Physics queries (OverlapSphere/ComputePenetration) з LateUpdate** — можна, але це main-thread і з `autoSyncTransforms=false` колайдери анімованих кісток ще не синхронізовані з поточним кадром **[SRC: Physics.SyncTransforms docs]**. Лише для рідкісних world-колізій.
- **Custom math proxies (sphere/capsule/plane)** — дешево, Burst-friendly, детерміновано, повний контроль (radius, "inside" mode, per-chain колайдер-список). Це те, що роблять *усі* spring-bone бібліотеки (VRM: sphere/capsule + extended collider (plane, inside) **[SRC: VRMC_springBone_extended_collider-1.0]**; naelstrof: sphere/capsule/plane, personal + scene colliders з broad phase grid **[CODE]**; UnityChan: sphere/capsule/panel **[CODE]**).
- **Penetration correction:** проєкція позиції (PBD-style) + тангенційне тертя; після корекції — повторно застосувати angle limit (UniVRM так і робить: `Anglelimit.Apply` після кожного колайдера **[CODE]**).
- **Collision constraints (XPBD з compliance)** — "м'які" колізії, потрібні лише в складних soft body.

**Рекомендація:** custom sphere/capsule/plane proxies, per-character список ("personal colliders"), перевірка і точки, і сегмента (head→tail) проти колайдера, 1 ітерація на substep. Найкраще співвідношення якість/ціна **[ENG]**.

### 8.3 Деталі реалізації
- Сегмент-капсула: closest points between two segments (Ericson, *Real-Time Collision Detection*) — готова реалізація в naelstrof `ClosestPointsOnTwoSegments` **[CODE]**.
- Radius колайдера масштабувати `lossyScale` (max abs компоненти) — UniVRM так робить **[CODE]**.
- Колізія може "заклинити" tail з іншого боку колайдера при великому кроці → substeps і velocity clamp.

---

## 9. Production architecture

### 9.1 Модулі
```
Runtime/
  Data/
    JiggleProfile            (ScriptableObject) — параметри + криві, версіонування
    JiggleColliderShape      (struct) — Sphere / Capsule / Plane, radius, height, inside flag
  Authoring (MonoBehaviour)/
    JiggleRig                — на корені персонажа: список chain'ів, колайдерів, LOD-налаштування, API Reset/Teleport/SetWeight
    JiggleChain (serializable class в JiggleRig) — root bone, end offset, profile, mirror, collider mask, weight
    JiggleColliderProxy      — компонент на кістках рук/торсу
  Simulation (plain C# / Burst)/
    JiggleWorld              — singleton-менеджер: реєстр рігів, NativeArrays, TransformAccessArray, time accumulator, scheduling
    JiggleSolver             — static Burst-функції: integrate, constraints, collide, pose
    JiggleState (struct)     — per-particle: pos, prevPos, restLocalRot, restLocalPos, lastWrittenLocal*, flags
    JiggleParams (struct)    — "запечені" з профілю: ω, ζ, drag per step, limits
  Editor/
    JiggleRigEditor, gizmos, preview-in-edit-mode, profile inspector з графіком step response
```

### 9.2 Обов'язкові функції (з вимог) → де реалізовано
| Вимога | Реалізація |
|---|---|
| кілька кісток, симетрія | `JiggleChain[]`, mirror-пара, per-chain profile |
| різні профілі, runtime parameters | профіль → `JiggleParams` bake; `JiggleRig.SetProfile(chain, profile, blendTime)`; `SetParamOverride` (множники amplitude/stiffness) |
| ScriptableObject presets | `JiggleProfile` + пресети (розділ 11.4) |
| enable/disable | `OnEnable/OnDisable` → register/unregister у `JiggleWorld`; при enable — reset |
| LOD | `JiggleWorld` рахує LOD-рівень per rig (distance до камери, visibility, екранний розмір): Full → Reduced rate → Frozen (static rest) |
| reset після teleport | `JiggleRig.Teleport(keepMotion)` + автодетект (distance/angle per frame threshold) |
| reset після scene load / respawn / pooling | reset у `OnEnable`, `SceneManager.sceneLoaded`, API `ResetSimulation()` для пулу |
| pause, timeScale=0 | dt=0 → не інтегрувати, але *застосовувати* останню позу (щоб кістки не повертались до rest/animated); опція UnscaledTime |
| Animator state changes | зміна пози = зміна target, velocity clamp гасить стрибок; опційно `Animator`-event для короткого blend weight |
| різкі переміщення | speed limit якоря + world inertia + max displacement |
| фізичні зіткнення | proxy colliders; опційно `AddImpulse(worldPoint, impulse)` для хіт-реакцій |
| різні FPS | fixed substep + cap + interpolation |
| editor preview | `[ExecuteAlways]` + `EditorApplication.update` для симуляції в edit mode з тестовим рухом якоря; gizmos rest/current/limits |

### 9.3 Дані профілю (JiggleProfile)
```
frequency (Hz)            1.5 – 5
dampingRatio (ζ)          0.1 – 1.0
response (r)              опційно
gravityScale              0 – 1 (× Physics.gravity), world-space
worldInertia              0 – 1 (0 = тільки анімація, 1 = повна світова інерція)
localInertia              0 – 1 (чутливість до руху батька/анімації)
anchorSpeedLimit (m/s)    напр. 6
anchorAngularLimit (deg/s) напр. 720
maxAngle (deg)            5 – 35
maxDisplacement (m)       0.005 – 0.06
translationWeight         0 – 1 (частка руху, що йде у translation кістки vs rotation)
stretch/squash            0 – 0.3
collisionRadius (m)       0.03 – 0.08
blend (weight)            0 – 1
axisWeights (vec3, local) — анізотропія: вертикаль > горизонталь > вперед-назад
```

---

## 10. Edge cases (реальні) і фікси

| Проблема | Причина | Фікс |
|---|---|---|
| Груди "вибухають" після teleport | Verlet: `x - x_prev` = відстань телепорту → величезна швидкість | Детект `|Δanchor| > teleportDistance` або `angle > teleportAngle` за кадр → **Reset** (x=prev=target) або **Keep** (зсунути x і prev на ту саму дельту/поворот). Magica має саме ці два режими Reset/Keep **[SRC]**; API `Teleport()` для явного виклику. |
| Різкий розворот на 180° | великий кутовий Δ якоря за кадр | anchor angular speed limit: надлишок повороту переносити в історію (обертати prev/x навколо якоря на "надлишковий" кут); maxAngle |
| Після respawn фізика "летить в космос" | стан з попереднього життя + телепорт + пул | reset в `OnEnable`, в `Respawn()`; ніколи не зберігати стан через `OnDisable` |
| Висока швидкість персонажа → неконтрольоване розтягнення | world-space інерція без ліміту | `worldInertia < 1`, anchorSpeedLimit, maxDisplacement, velocity clamp |
| Jiggle залежить від FPS | per-frame коефіцієнти (`*= 1-d`, `Lerp(k)`) | fixed substep + cap + interpolation; всі коефіцієнти — "на фіксований крок" |
| Тремтіння при 30 FPS | один великий крок, або стрибаючий якір з FixedUpdate | substeps (напр. 90 Гц → 3 кроки при 30 FPS) з **інтерполяцією якоря між минулим і поточним кадром** всередині substeps |
| Animator перезаписує кістки | запис у Update/FixedUpdate | запис лише в LateUpdate; reset-логіка з 6.3 |
| Неанімовані кістки "дрейфують" | feedback: наш вихід стає входом | 6.3 фікс Б (порівняння з lastWritten) |
| Root motion дає імпульси | ривки на переходах | velocity clamp, speed limit, world inertia |
| Ragdoll | анімація вимкнена, тіло рухає PhysX | залишити jiggle (якір = chest rigidbody transform, interpolation ON), або на час ragdoll перейти на ConfigurableJoint; при вході/виході — reset або короткий blend |
| Pause/unpause → великий delta | `Time.deltaTime` після паузи (обмежений `Time.maximumDeltaTime`, дефолт 0.333 с) або unscaled | cap кроків (напр. 4), відкидання залишку; при dt > 0.25 — "soft reset" (обнулити швидкість) |
| `Time.timeScale = 0` | dt=0 | не інтегрувати; *писати* останню позу щоб Animator (який теж стоїть) не бачив змін; у меню з анімацією на UnscaledTime — режим unscaled |
| Зміна animation state → snap | target стрибає за кадр | velocity clamp; опційно — короткий blend `physicsWeight` або "inertialization" target'у |
| Camera-relative motion | симуляція в camera space або camera як parent | симуляція в world/anchor space; камера ніколи не в ієрархії персонажа |
| Scale персонажа ≠ 1 | довжини/радіуси/спринг у метрах | всі відстані множити на `lossyScale` якоря (UniVRM `scalingFactor = cmax(abs(lossyScale))` **[CODE]**); частота і ζ від scale не залежать |
| Mirrored rigs / negative scale | FromTo + негативний scale інвертує handedness | визначати bone axis з даних; перевіряти `determinant(localToWorld) < 0` і логувати помилку в редакторі; виправляти експорт |
| Parent transform scale (non-uniform) | skew у матрицях → кватерніони некоректні | заборонити non-uniform scale у батьках jiggle (валідація в редакторі) |
| Character pooling | стан від попереднього використання | `ResetSimulation()` в `OnSpawnFromPool` |
| Scene reload / domain reload off | статичний менеджер тримає мертві Transforms | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` для очистки статики (naelstrof так робить **[CODE: JigglePhysics.Initialize]**); перевірка `TransformAccess.isValid` |
| Physics timestep змінюється | солвер прив'язаний до fixedDeltaTime | власна константа кроку |
| Великий hitch (завантаження) | dt 0.3 с | cap + "soft reset" |
| Кілька камер / split-screen | LOD по одній камері | LOD по мінімальній відстані до будь-якої активної камери |
| Timeline/катсцена з baked jiggle | подвійний jiggle | weight → 0 на час катсцени через `JiggleRig.SetWeight` з Timeline track або signal |
| NaN | ділення на 0 у normalize | `normalizesafe`, NaN guard → reset частинки (як UnityChan **[CODE]**) |

---

## 11. Параметри: "збільшую X → отримую Y"

| Параметр | ↑ збільшення → візуально | ↓ зменшення → візуально | Типова помилка |
|---|---|---|---|
| **Stiffness / frequency (Hz)** | швидші, дрібніші коливання, "пружні", менше провисання від gravity | повільні, "важкі", "желейні" хвилі, більше sag | ставити frequency вище ~6–8 Hz при 60 Гц кроці без substeps → нестабільність/тремтіння |
| **Damping / damping ratio ζ** | менше коливань, рух швидко заспокоюється; при ζ≥1 — без overshoot, "в'язко" | більше коливань, довге "дзеленчання" | ζ ≈ 0.05 → нескінченний jiggle на idle від дихання |
| **Mass** (якщо є окремо) | повільніший відгук на сили і колізії при тій самій k | швидший, "легкий" | змінювати mass замість frequency — ламає тюнінг |
| **Gravity** | сильніше провисання, особливо у нахилі/лежачи | форма тримається як змодельована | подвійний sag (модель вже провисла) |
| **Inertia (world)** | сильна реакція на біг/стрибки/транспорт | реагує тільки на анімацію | 1.0 + без speed limit → розтяг при спринті/телепорті |
| **Inertia (local)** | сильна реакція на повороти тулуба/анімацію | мало реакції на анімацію | — |
| **Drag (air/world)** | "у воді", гасить світовий рух | вільний рух | per-frame drag без dt |
| **Elasticity** (Dynamic Bone/naelstrof) | сильніше тяга до пози (схоже на stiffness) | вільніше | змішування з stiffness в одному пресеті без розуміння |
| **Max angle** | більша свобода амплітуди | "обрізаний" рух, видно "упор" | занадто жорсткий hard clamp → різкі зупинки; використовувати soft limit |
| **Max displacement** | більший вертикальний bounce | стриманий рух | для translation-режиму обов'язковий |
| **Frequency ↔ Response** (t3ssel8r r) | r>1: "перестрибує" на старті | r<0: "anticipation" | — |
| **Softness** (angle/limit soften) | м'якший підхід до ліміту | жорсткий упор | — |
| **Collision radius** | груди "товщі" для колайдерів, раніше впираються в руку | проходять крізь руку | радіус більший за реальний → груди "відскакують" від руки на відстані |

### 11.1 Відображення параметрів між бібліотеками (для міграції)
- Dynamic Bone: Damping, Elasticity, Stiffness, Inert, Radius, End Length/Offset, Gravity, Force, Update Rate, Distant Disable (з документації/інспектора) **[SRC: asset page / Will Hong blog]**.
- VRM: `stiffness`, `dragForce`, `gravityPower`, `gravityDir`, `hitRadius`, `center` **[SRC: spec]**. Користувачі скаржаться, що 2 параметри (stiffness, drag) не дозволяють відтворити DynamicBone (3 параметри) **[SRC: https://github.com/vrm-c/UniVRM/issues/2331]**.
- VRChat PhysBones: Pull (повернення до rest), Spring/Momentum, Stiffness, Gravity, Immobile (гасить рух від root parent) **[SRC: https://creators.vrchat.com/common-components/physbones/]** — Immobile ≈ (1 - worldInertia).
- naelstrof: angleElasticity, lengthElasticity, rootElasticity, elasticitySoften, drag, airDrag, gravityMultiplier, ignoreRootMotion, angleLimit, angleLimitSoften, collisionRadius, blend (усі 0..1) **[CODE]**.
- Magica BoneSpring: Gravity, Drag, Spring, Rotation adjustment, Clamp Position, World Influence/Inertia, Max Velocity; пресет "Breast" **[SRC]**.

### 11.2 Пресети (стартові значення, [ENG], тюнити на моделі)
Позначення: f — Hz, ζ, g — gravityScale, wI — world inertia, lI — local inertia, θmax — max angle, dmax — max displacement (м, при scale 1), tW — translation weight.

| Пресет | f | ζ | g | wI | lI | θmax | dmax | tW | Характер |
|---|---|---|---|---|---|---|---|---|---|
| **Subtle** (щільний одяг, реалістичні ігри) | 3.5 | 0.70 | 0.10 | 0.30 | 0.6 | 8° | 0.010 | 0.2 | ледь помітний відгук на кроки, 1 коливання |
| **Realistic** | 2.5 | 0.45 | 0.25 | 0.60 | 0.8 | 15° | 0.025 | 0.4 | 2–3 коливання після стрибка, помітний bounce при бігу |
| **Exaggerated** | 1.8 | 0.25 | 0.35 | 1.00 | 1.0 | 25° | 0.045 | 0.5 | багато коливань, великий sag |
| **Anime/cartoon** | 3.0 | 0.20 | 0.15 | 0.80 | 1.0 | 20° | 0.035 | 0.6 + squash/stretch 0.15, вертикаль ×1.5 | "пружний", швидкий, анізотропний |
| **Heavy movement** (бій, паркур) | 3.0 | 0.60 | 0.20 | 0.50 | 0.7 | 12° | 0.020 | 0.3, anchorSpeedLimit 5 м/с, angular 540°/с | стабільно при ривках, без розтягу |

Перевірка пресетів: сцена-стенд з (а) idle-диханням, (б) ходьба/біг, (в) стрибок і приземлення, (г) розворот 180° за 0.1 с, (д) teleport, (е) 30/60/144 FPS однаково.

---

## 12. Optimization

### 12.1 Вартість складових
- **Rigidbody/Joint:** кожне тіло в PhysX scene + sync transforms; для 100 персонажів × 2 = 200 тіл + joints — відчутно, особливо на мобільних. Плюс з `autoSyncTransforms` кожне переміщення transform + query дає sync-точку **[SRC: Physics.autoSyncTransforms docs]**.
- **Transform updates:** запис у `Transform` — найдорожча частина простих солверів (dirty-флаги, ієрархія, SkinnedMeshRenderer). Мінімізуйте записи: `SetLocalPositionAndRotation` одним викликом, не писати, якщо не змінилось (але див. 6.3 — тоді треба інакше детектити анімацію).
- **GC:** жодних `new`, LINQ, `GetComponent` у кадрі; лямбд/замикань; `foreach` по List без struct enumerator в старих рантаймах. UnityChan `SpringBone.cs` використовує LINQ лише при ініціалізації **[CODE]**.
- **Jobs + Burst + Unity.Mathematics:** солвер як `IJobFor`/`IJobParallelFor` по chain'ах (UniVRM паралелить по "spring" — рівень ланцюга **[CODE]**), читання/запис трансформів через `IJobParallelForTransform` + `TransformAccessArray`.
  - Важливо: `IJobParallelForTransform` паралелиться **по root-ієрархіях**; трансформи під одним root обробляються одним потоком **[SRC: https://medium.com/toca-boca-tech-blog/unitys-transformaccessarray-internals-and-best-practices-2923546e0b41]**. Тобто 1 персонаж ≈ 1 потік на запис; користь від паралельності — лише при багатьох персонажах.
  - naelstrof розділяє: bulk read (ScheduleReadOnly) → simulate (Burst `IJobFor` по деревам) → interpolate → write; колайдери зчитуються окремими read-only jobs; broad phase — hash grid **[CODE: JiggleJobs.cs]**. Заявлено "600 jiggling Stanford Armadillos within fractions of a millisecond" **[SRC: README]** (маркетингове твердження автора, не незалежний бенчмарк).
- **DOTS/ECS:** не потрібні. Jobs+Burst дають майже всю вигоду без переходу на Entities. uSpringBone (ECS) — старий (2018) приклад **[SRC]**.
- **LOD:**
  - L0 (близько, видно): повна частота (напр. 90 Гц), колізії.
  - L1 (середня відстань): 30–45 Гц або 1 substep, без колізій з руками.
  - L2 (далеко): вимкнути, писати rest/animated pose (тобто нічого не писати).
  - Offscreen: `Renderer.isVisible` / `CullingGroup` API / `OnBecameInvisible` → freeze; при поверненні — reset (бо Animator міг теж бути culled).
- **Sleep:** якщо якір не рухався і енергія (|v|) нижче порогу N кадрів — пропускати інтеграцію (EZSoftBone має `sleepThreshold` **[CODE]**).
- **Simulation frequency reduction** — дає лінійну економію; обов'язково з interpolation, інакше видно "степи".
- **Batching:** один менеджер → один `LateUpdate` → один schedule; уникати N MonoBehaviour-апдейтів.

### 12.2 Що потрібно для масштабу
| Кількість | Що робити |
|---|---|
| **1 персонаж** (hero, dating sim, fighting) | MonoBehaviour-солвер у LateUpdate, без алокацій. Можна дорожчу якість: 2 кістки на сторону, колізії з руками, blendshape-корективи. Jobs не потрібні. |
| **10** | Один `JiggleWorld` менеджер; кешовані масиви; LOD по відстані; visibility culling. Все ще main-thread OK (~0.05–0.2 мс залежно від платформи — **міряти Profiler'ом**). |
| **50** | Burst + Jobs (simulate), `TransformAccessArray` для read/write, LOD 3 рівні, зменшена частота для L1, без колізій на L1+. |
| **100+** | Все вище + агресивний culling (тільки N найближчих повна якість — "budget"), sleep, персональні колайдери лише для L0, шарування оновлень (половина L1-персонажів в парних кадрах, половина — в непарних) з interpolation, scene-колайдери вимкнені. Профілювати на цільовому мобільному пристрої. |

---

## 13. Multiplayer

### 13.1 Варіанти
| Варіант | Трафік | Якість | Коли |
|---|---|---|---|
| Synchronize bones (кожен кадр/тік) | великий (2+ кватерніони × персонажі × тік) | гірше за локальну (тік-рейт < FPS, інтерполяція вбиває jiggle) | ніколи |
| Synchronize parameters (профіль, outfit, weight) | мізерний, подієвий | локальна симуляція | коли параметри змінюються геймплеєм |
| Local cosmetic simulation | 0 | найкраща | **за замовчуванням завжди** |
| Deterministic reconstruction | 0 (лише входи) | ідентична на всіх клієнтах | лише replay-системи/lockstep, де потрібен побітовий збіг; вимагає фіксованого кроку, детермінованої математики (Burst FloatMode.Deterministic — [перевірити для вашої версії]) і однакових входів |

### 13.2 Висновок
Jiggle — **завжди cosmetic client-side**, якщо лише він не впливає на геймплей (не впливає ніколи для грудей). Сервер (dedicated/headless) jiggle не рахує взагалі — вимикати систему на `#if UNITY_SERVER` або при batchmode.

### 13.3 Що важливо для кожної мережевої бібліотеки
Суть одна: **вхід для jiggle (трансформ персонажа і анімація) на proxy має бути гладким, з частотою рендеру**. Якщо proxy "стрибає" з частотою тіку (напр. 30 Гц) — jiggle отримує імпульси щотіку і тремтить.
- **Photon Fusion 2:** стан рендериться через інтерполяцію між снапшотами в `Render()`; `FixedUpdateNetwork` за замовчуванням не виконується на proxies; без інтерполяції proxy "would appear to animate and update at the tick rate" **[SRC: https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop]**. Jiggle — у `LateUpdate` після того, як Fusion застосував інтерпольований трансформ. Resimulation (prediction rollback) для локального гравця — jiggle не повинен "перезапускатись" при resim: він живе в render-часі, не в tick-часі.
- **Netcode for GameObjects:** `NetworkTransform` інтерполює на non-authority клієнтах **[SRC: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/components/networktransform.html]** — тримати interpolation увімкненою для персонажів; `NetworkAnimator` синхронізує параметри аніматора, кістки не треба.
- **Mirror:** `NetworkTransform` з snapshot interpolation **[SRC: https://mirror-networking.gitbook.io/docs/manual/components/network-transform/snapshot-interpolation]**.
- **Телепорти/респавни proxy** (snap при великій похибці) → автодетект teleport у jiggle обов'язковий, інакше груди "вибухають" на чужих клієнтах.
- Власник (local player) з client-side prediction і reconciliation: корекція позиції → стрибок якоря → той самий детектор + speed limit.

---

## 14. GitHub implementations (з аналізом коду)

### 14.1 naelstrof/JigglePhysics — **рекомендовано вивчити першим**
- URL: https://github.com/naelstrof/JigglePhysics · License **MIT** · ⭐584 · останній push **2026-05-05** (активний) · UPM `com.gator-dragon-games.jigglephysics`.
- Алгоритм **[CODE: Packages/com.gator-dragon-games.jigglephysics/Scripts/JiggleJobSimulate.cs]**:
  - Verlet на фіксованому кроці (`deltaTimeSquared = fixedDeltaTime²`), gravity `* gravityMultiplier * dt²`.
  - Розділення швидкості на локальну (відносно батька) з `drag` і глобальну з `airDrag`.
  - `ignoreRootMotion`: зсув історії на дельту кореня (керування world inertia).
  - Constraints: angle (lerp до пози, повернутої FromTo батьківського aim) з `elasticitySoften` (pow похибки), length elasticity, angle limit (аналітичне трикутне рішення), back-propagation для колізій, спеціальний root-solve з `rootElasticity`.
  - Колізії: sphere/capsule/plane, personal + scene колайдери, broad phase hash grid.
  - Pose: FromTo(animated→simulated) з `blend` (slerp) × input rotation; multi-children — усереднення напрямків.
  - Timestep: `ScheduleSimulate` у LateUpdate, наздоганяє час, але робить ≤1 крок/кадр; interpolation між двома останніми станами з 2-кроковою затримкою + `snapToReal` для кореня **[CODE: JigglePhysics.cs, JiggleJobInterpolation.cs]**.
  - Reset/Animator: `JiggleJobBulkTransformReset` — детект змін local pose (анімовано/не анімовано) **[CODE]**.
  - Статичний стан очищується в `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` (domain reload off safe) **[CODE]**.
- Плюси: Burst/Jobs, масштабується, продумана Animator-інтеграція, колізії, MIT. Мінуси: параметри не фізичні (0..1, "authorable rather than physically accurate" **[SRC: README]**), ≤1 крок/кадр означає, що при FPS < 1/fixedDeltaTime симуляція сповільнюється в часі; складний код (unsafe, пам'ять фрагментована вручну).
- Production: **так**.

### 14.2 vrm-c/UniVRM (SpringBone, FastSpringBone)
- URL: https://github.com/vrm-c/UniVRM · MIT · ⭐3387 · push 2026-08-20 (активний). Специфікація: https://github.com/vrm-c/vrm-specification/blob/master/specification/VRMC_springBone-1.0/README.md
- Алгоритм **[CODE: Packages/UniGLTF/Runtime/SpringBoneJobs/UpdateFastSpringBoneJob.cs]**: Verlet, `(cur-prev)*(1-drag)` + stiffness-напрямок `* dt` + external `* dt`, length constraint, angle limits (cone/hinge/spherical), sphere/capsule(+extended) колізії, `center` space, runtime scaling. `IJobParallelFor` по ланцюгах, опційний Burst (`ENABLE_SPRINGBONE_BURST`).
- Update modes: `LateUpdate` (Time.deltaTime), `FixedUpdate` (fixedDeltaTime), `Manual` **[CODE: VRM/Runtime/SpringBone/VRMSpringBone.cs]**. `StopSpringBoneWriteback` — логіка продовжується без запису, щоб "не бушувало" при відновленні **[CODE]**.
- Плюси: стандарт VRM, стабільний, джоби. Мінуси: frame-rate dependent (drag per frame, stiffness×dt), 2 параметри — мало контролю для грудей.
- Production: так для VRM-аватарів; для кастомної гри — як референс.

### 14.3 unity3d-jp/UnityChanSpringBone
- URL: https://github.com/unity3d-jp/UnityChanSpringBone · MIT · ⭐533 · push 2023-10 (малоактивний).
- **[CODE: Runtime/SpringBone.cs, SpringManager.cs]**: Hooke + Verlet (`force *= 0.5*dt²`, `(1-drag)*(cur-prev)`), length/angle limits, sphere/capsule/panel колайдери, NaN reset, `dynamicRatio` для анімованих кісток, **фіксований dt = 1/simulationFrameRate на кадр** (slow-mo при низькому FPS).
- Корисно як навчальний код (Hooke явно). Production: з доопрацюванням timestep.

### 14.4 EZhex1991/EZSoftBone
- URL: https://github.com/EZhex1991/EZSoftBone · MIT · ⭐510 · push 2023-11.
- **[CODE: Runtime/EZSoftBone.cs]**: velocity-based, параметри damping/stiffness/resistance/slackness як криві по глибині (material asset), iterations, sleepThreshold, simulateSpace, `RevertTransforms` у Update. Frame-rate dependent коефіцієнти.
- Популярний у tutorial'ах для грудей (відео UnityQueenGame). Production: для невеликих проєктів.

### 14.5 OneYoungMean/Automatic-DynamicBone
- URL: https://github.com/OneYoungMean/Automatic-DynamicBone · MIT · ⭐1178 · push 2024-12. Базується на SPCRJointDynamics; Jobs + Burst, автогенерація кісток/колайдерів, sphere/capsule/box колайдери, субкроки ("iteration"), всі платформи крім WebGL **[SRC: README]**. Документація переважно китайською. Production: з обережністю (preview-версія 2.0, автор пише про можливу нестабільність).

### 14.6 SPARK-inc/SPCRJointDynamics
- URL: https://github.com/SPARK-inc/SPCRJointDynamics · cross-simulation (mass-spring-damper) для hair/tail/breast/skirt, Unity 2018.2+ **[SRC]**. Орієнтований на одяг/зачіски (сітки кісток).

### 14.7 Інші (довідково)
- TheAllenChou/numeric-springing (MIT, 2020) — чисті numeric springs **[OLD]**: https://github.com/TheAllenChou/numeric-springing
- TheAllenChou/unity-cj-lib `NumericSpring.cs` (MIT) **[OLD]**.
- EsProgram/uSpringBone (BSD-3, 2018, ECS) — **[OLD]**, мертвий, лише як приклад ECS.
- yangrc1234/SpringBone (MIT, 2017) — **[OLD]**.
- dreamfairy/Unity-DynamicBone-JobSystem-Opmized (2020) — **[OLD]**, без ліцензії в метаданих — не використовувати в продакшні.
- Unity-Technologies/animation-jobs-samples (2020) — приклади `IAnimationJob` (у т.ч. damping) **[OLD]**: https://github.com/Unity-Technologies/animation-jobs-samples
- Jiggle Chain constraint (GDC 2019, Google Drive, не компілюється з AR ≥1.0.3 без правок) — **сумнівно**, лише як ідея **[SRC: discussions]**.
- detomon/wigglebone (Godot, MIT, активний) і cheece/JiggleArmature (Blender) — корисні для порівняння алгоритмів, не Unity.
- Репозиторії за запитом "breast physics" на GitHub — практично відсутні/порожні (1 репозиторій з 0 зірок, VNyan-плагін) — **нерелевантні**.

---

## 15. Assets/tools (Asset Store та інші)

Ціни — на дату перевірки (2026-09-19), Asset Store часто має знижки.

| Назва | URL | License | Ціна | Unity | Статус | Алгоритм | Плюси | Мінуси | Production | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| **Magica Cloth 2** (BoneSpring / BoneCloth / MeshCloth) | https://assetstore.unity.com/packages/tools/physics/magica-cloth-2-242307 | Asset Store EULA | $38.50 (у момент перевірки знижка $19.25) | 2021.3.20+ / 2022.3+ | **активний** (v2.18.3, 2026-09-03) | PBD-подібний, Burst/Jobs ("DOTS technology") | BoneSpring з пресетом **Breast**, world/local inertia, speed limits, teleport Reset/Keep, `ResetCloth()`, режим Unity Physics | закритий алгоритм (але C# source входить в пакет), великий пакет | **так** | так (C# у пакеті) |
| **Dynamic Bone** (Will Hong) | https://assetstore.unity.com/packages/tools/animation/dynamic-bone-16743 | EULA | $20 | 2019.4.1+ | оновлення рідкі (v1.3.4, 2024-05) | Verlet, UpdateRate | простий, класика, source включено | застарілий, не Burst, повільніший на масштабі | так для невеликих проєктів | так |
| **Boing Kit** (Long Bunny Labs) | https://assetstore.unity.com/packages/tools/particles-effects/boing-kit-dynamic-bouncy-bones-grass-and-more-135594 | EULA | ~$60 | 2019.4+ | оновлення рідкі | numeric springs, Jobs, GPU reactors | dynamic bouncy bones + effectors | дорогий, орієнтований на VFX | так | так |
| **Obi Softbody** (Virtual Method) | https://assetstore.unity.com/packages/tools/physics/obi-softbody-130029 | EULA | ~$55 | див. сторінку | активний (Obi suite) | particle-based shape matching/XPBD | справжній soft body, collisions | дорого по CPU, складно | тільки hero-персонажі | так |
| **UniVRM SpringBone** | https://github.com/vrm-c/UniVRM | MIT | free | 2022.3+ (див. README) | активний | Verlet | стандарт VRM | 2 параметри, FPS-dependent | так (VRM) | так |
| **naelstrof JigglePhysics** | https://github.com/naelstrof/JigglePhysics | MIT | free | див. package.json | активний | Verlet + Burst | див. 14.1 | див. 14.1 | так | так |
| **EZSoftBone** | https://github.com/EZhex1991/EZSoftBone | MIT | free | — | малоактивний | velocity + lerp | простий | FPS-dependent | обмежено | так |
| **UnityChanSpringBone** | https://github.com/unity3d-jp/UnityChanSpringBone | MIT | free | — | малоактивний | Verlet | навчальний | фікс. dt/кадр | обмежено | так |
| **Tools for Magica Cloth 2** | https://assetstore.unity.com/packages/tools/physics/tools-for-magica-cloth-2-free-311633 | EULA | free | — | — | доп. інструменти | — | third-party | — | — |
| **VRChat PhysBones** | https://creators.vrchat.com/common-components/physbones/ | VRChat SDK terms | free | лише VRChat SDK | активний | — | хороші ідеї параметрів (Pull/Spring/Immobile) | **не для власних ігор** | ні | ні |

Unity Cloth (built-in) — не рекомендовано для тіла (див. 4.7).

---

## 16. Найкращі tutorials / відео

Відібрано по одному-два найкращі на підтему. Тривалість/дата — де вдалося перевірити; YouTube-метадані отримано через oEmbed (назва/автор перевірені).

| Підтема | Ресурс | Автор | URL | Дата | Рівень | Що вивчається | Код | Актуальність |
|---|---|---|---|---|---|---|---|---|
| Математика пружин / процедурна анімація | Giving Personality to Procedural Animations using Math | t3ssel8r | https://www.youtube.com/watch?v=KPoeNZZ6H4s | 2022 | середній | second-order system (f, ζ, r), semi-implicit, stabільність, dt | формули у відео; транскрипт-код: github SalvatoreScalia | **висока** (математика вічна) |
| Springs для геймдеву (текст) | Spring-It-On | Daniel Holden | https://theorangeduck.com/page/spring-roll-call | ~2021 | середній–просунутий | damper→spring, half-life, exact solutions, quaternion springs, inertialization | так (C++) | висока |
| Numeric springing (текст) | Game Math: Numeric Springing (серія) | Allen Chou | https://allenchou.net/2015/04/game-math-precise-control-over-numeric-springing/ | 2015 **[OLD]** | початковий–середній | f, ζ, implicit Euler, half-life | так (C#) | висока |
| Verlet + constraints | Advanced Character Physics (GDC 2001) | Thomas Jakobsen | https://www.cs.cmu.edu/afs/cs/academic/class/15462-s13/www/lec_slides/Jakobsen.pdf | 2001 **[OLD]** | середній | Verlet, relaxation, collision by projection | псевдокод | висока (основа spring bones) |
| XPBD / soft body | Ten Minute Physics 09 XPBD; 10 Soft bodies | Matthias Müller | https://www.youtube.com/watch?v=jrociOAYqxA ; https://www.youtube.com/watch?v=uCaHXkS2cUg ; PDF: https://matthias-research.github.io/pages/tenMinutePhysics/09-xpbd.pdf | 2022 | середній | XPBD, compliance, soft body | так (JS) | висока |
| Fixed timestep | Fix Your Timestep! | Glenn Fiedler | https://gafferongames.com/post/fix_your_timestep/ | 2004 **[OLD]** | початковий | accumulator, interpolation, spiral of death | так | висока |
| Animation Rigging (офіційно) | Introducing the New Animation Rigging Features (GDC 2019) | Unity Technologies | https://www.gdcvault.com/play/1026151/ | 2019 | середній | constraints, physics-based secondary motion (jiggle chain) | семпли | середня (API змінився) |
| Animation Rigging (серія) | Intro to Animation Rigging & Procedural Animation in Unity | iHeartGameDev | https://www.youtube.com/watch?v=Wx1s3CJ8NHw | ~2022 | початковий | Rig Builder, constraints, pipeline | так | висока |
| Unity Learn | Using Animation Rigging: Damped Transform | Unity Learn | https://learn.unity.com/course/prototyping-a-procedural-animated-boss/tutorial/using-animation-rigging-damped-transform | — | початковий | DampedTransform для secondary motion | проєкт | висока |
| Розширення AR на C# | Extending the Animation Rigging package with C# (Unite Copenhagen 2019) | Unity | https://www.slideshare.net/slideshow/extending-the-animation-rigging-package-with-c-unite-copenhagen-2019/180510356 | 2019 | просунутий | IAnimationJob, binder, custom constraints | так | середня |
| Magica Cloth 2 | MagicaCloth2 BoneCloth Tutorial | Magica Soft | https://www.youtube.com/watch?v=vNtJxBUgSaI | — | початковий | налаштування BoneCloth | — | висока |
| Magica BoneSpring (текст) | BoneSpring Start Guide | Magica Soft | https://magicasoft.jp/en/magicaclothbonespringstart-2/ | — | початковий | chest spring, пресет Breast | — | висока |
| Блендер-превью jiggle | Jiggle Physics (addon) + відео | naelstrof / Alex CGW | https://extensions.blender.org/add-ons/jiggle-physics/ ; https://www.youtube.com/watch?v=5HRzKJXpqfY | 2025 | початковий | jiggle-кістки в Blender | addon | висока |
| Quick Unity setup (low bar) | How to Add Hair & Boob Jiggle in Unity With One Free Plugin! (EZSoftBone) | UnityQueenGame | https://www.youtube.com/watch?v=eQMudj9VQ7Q | 2025 | початковий | Blender→Mixamo→Unity + EZSoftBone | ні | середня (не production-підхід) |

Пропущено навмисно: випадкові "jiggle in 30 seconds" відео — не пояснюють ні timestep, ні reset.

---

## 17. Найкращі курси / навчальні матеріали

Курсу саме про breast physics немає. Складові навички:

| Навичка | Курс/матеріал | URL | Коментар |
|---|---|---|---|
| Фізичне моделювання (фундамент) | Witkin & Baraff, *Physically Based Modeling* (SIGGRAPH 2001 course notes) **[OLD]** | https://graphics.pixar.com/pbm2001/ | ODE, integrators, springs, constraints, collision — класика, безкоштовно |
| Physics-based animation (академ.) | physicsbasedanimation.com — Resources & Courses | https://www.physicsbasedanimation.com/resources-courses/ | агрегатор курсів SIGGRAPH |
| XPBD/soft body/cloth | Ten Minute Physics (весь курс) | https://matthias-research.github.io/pages/tenMinutePhysics/index.html | найкращий практичний курс по симуляції |
| PBD/XPBD папери | Müller et al. PBD (2007), Macklin et al. XPBD (2016) | https://dl.acm.org/doi/pdf/10.1145/2994258.2994272 | для Advanced-варіанту |
| Unity Animation Rigging | Unity Learn "Working with Animation Rigging" | https://learn.unity.com/tutorial/working-with-animation-rigging?language=en | офіційно |
| Unity animation + physics (платні) | Udemy "Advanced Unity Game Development and Systems Design" (оновлено 2026-08; IK, Animation Rigging, ragdoll, joints) | https://www.udemy.com/course/advanced-unity-game-development-and-systems-design/ | загальний, не про jiggle; перевірити програму перед покупкою |
| | Udemy "Animating in Unity" (оновлено 2026-07; AR constraint samples) | https://www.udemy.com/course/animating-in-unity/ | базовий рівень |
| Jobs/Burst | Unity Manual: Job System, Burst; Toca Boca "TransformAccessArray internals" | https://medium.com/toca-boca-tech-blog/unitys-transformaccessarray-internals-and-best-practices-2923546e0b41 | критично для масштабу |
| Колізії | Christer Ericson, *Real-Time Collision Detection* (книга, 2004) **[OLD]** | — | closest point segment-segment, sphere/capsule |
| Game physics (книга) | Ian Millington, *Game Physics Engine Development* **[OLD]** | — | springs, particles, contacts |

GameDev.tv: окремого курсу про secondary motion/jiggle не знайдено — не вигадую.

---

## 18. Skills для AI coding agent

| Skill | Що знати | Навіщо | Джерело | Мін. рівень | Вправа |
|---|---|---|---|---|---|
| Unity lifecycle | Update/FixedUpdate/LateUpdate, execution order, `DefaultExecutionOrder`, OnEnable/OnDisable, domain reload off | вставити jiggle після Animator; reset | Unity Manual execution-order | впевнено | Логувати порядок викликів з Animator (Normal/Fixed/Unscaled) і показати, в якому кадрі видно результат |
| Animator internals | update modes, culling modes, bindings (які transforms пише), Write Defaults, root motion | уникнути перезапису/дрейфу | Unity docs AnimatorUpdateMode, Animator | впевнено | Сцена з анімованою і неанімованою кісткою: показати дрейф без reset і його усунення |
| Transform hierarchy | local vs world, lossyScale, TransformPoint/InverseTransformPoint, `SetLocalPositionAndRotation` | правильні простори | Unity ScriptReference Transform | впевнено | Перенести точку між chest-space і world при non-1 scale |
| Vector/quaternion math | FromToRotation, Slerp, axis-angle, кватерніонна експонента, handedness, normalize-safe | pose output, angle limits | Holden (quaternion springs), Unity.Mathematics | впевнено | Реалізувати `FromTo` і cone-limit без NaN при протилежних векторах |
| Spring-damper systems | ω, ζ, f, half-life, critically damped | тюнінг | Chou, Holden, t3ssel8r | глибоко | Графік step response для ζ=0.2/0.7/1.0 у EditMode-тесті |
| Numerical integration | explicit/semi-implicit/implicit Euler, Verlet, стабільність `ωh<2` | відсутність вибухів | Witkin&Baraff, Jakobsen | глибоко | Показати вибух explicit Euler і стабільність implicit на f=20Hz, h=1/30 |
| Fixed timestep | accumulator, interpolation, step cap | FPS independence | Gaffer | глибоко | Тест: однакова траєкторія при 30/60/144 FPS (похибка < 1%) |
| Constraints (PBD) | length, angle cone, max displacement, projection | стабільність і контроль | Jakobsen, Müller | середній | Реалізувати cone-limit і soft-limit через tanh |
| Collision math | point/segment vs sphere/capsule/plane, penetration depth | груди↔руки | Ericson; naelstrof code | середній | Unit-тести closest-points segment-segment |
| Rigging basics | pivot, bone roll, weights, Blender FBX export, humanoid/generic | правильні вхідні дані | Blender manual, Unity Avatar docs | базовий | Перевірити симетрію rest-осей `Breast_L/R` скриптом у редакторі |
| Animation Rigging | RigBuilder, constraint jobs, AnimationStream | альтернатива/інтеграція з IK | AR docs, Unite 2019 | базовий (для B), середній (для C) | Написати custom constraint з dt з `stream.deltaTime` |
| Jobs/Burst/Mathematics | NativeArray, IJobFor, IJobParallelForTransform, TransformAccessArray, Dispose, safety | масштаб 50–100+ | Unity docs, Toca Boca | середній | Перенести солвер у Burst-job і порівняти в Profiler |
| Memory/GC | zero-alloc hot path, struct-of-arrays | стабільний frame time | Unity Profiler docs | середній | Profiler: 0 B GC Alloc у LateUpdate |
| Profiling | Profiler, Profile Analyzer, ProfilerMarker, deep profile на пристрої | доказ оптимізацій | Unity docs | середній | Додати `ProfilerMarker` на кожну фазу |
| ScriptableObject/serialization | профілі, OnValidate, версія даних | presets | Unity docs | базовий | Профіль з `OnValidate` що перераховує bake-параметри |
| Editor tooling | `[ExecuteAlways]`, gizmos, Handles, custom inspector, EditorApplication.update | preview і тюнінг | Unity docs | базовий | Gizmo: rest/current/limit cone |
| Testing | EditMode (математика), PlayMode (FPS-інваріантність, teleport) | регресії | Unity Test Framework | базовий | Тест "teleport 1000 м → displacement < dmax" |
| Networking basics | authority, interpolation proxies, cosmetic vs gameplay state | не синхронізувати зайве | Fusion/NGO/Mirror docs | базовий | Proxy з 20 Гц тіком без/з інтерполяцією — порівняти jiggle |

---

## 19. Learning path

### Level 1 — мінімальна jiggle-система
- **Theory:** Hooke, damping, semi-implicit Euler; LateUpdate після Animator.
- **Resources:** t3ssel8r відео; Chou "Numeric Springing"; Unity execution order.
- **Task:** одна кістка: tail-точка зі спринговим прискоренням до анімованого tail, semi-implicit Euler з `Time.deltaTime`, rotation через FromTo у LateUpdate.
- **Expected:** груди реагують на рух chest; видно, що при 30 vs 144 FPS поведінка трохи різна; телепорт ламає.

### Level 2 — стабільна spring simulation
- **Theory:** f/ζ-параметризація, стабільність, fixed timestep + interpolation, Verlet, velocity clamp, NaN guard.
- **Resources:** Holden, Gaffer, Jakobsen.
- **Task:** власний accumulator (90 Гц, cap 4 кроки), interpolation якоря всередині substeps, інтерполяція виходу, Reset/Teleport API + автодетект.
- **Expected:** ідентична поведінка на 30/60/144 FPS (тест), teleport без вибуху, pause/timeScale=0 без стрибка.

### Level 3 — bone constraints + animation
- **Theory:** local/world inertia, speed limits, cone limits, max displacement, reset для неанімованих кісток, translation+rotation, squash.
- **Resources:** naelstrof `JiggleJobSimulate.cs`, `JiggleJobBulkTransformReset.cs`; UniVRM `UpdateFastSpringBoneJob.cs`; Magica inertia docs.
- **Task:** ланцюг з 2 кісток, worldInertia/localInertia, cone limit з soft зоною, режим детекту анімованості кістки.
- **Expected:** реакція на біг регульована одним слайдером; жодного дрейфу; анімовані кістки коректно змішуються з симуляцією.

### Level 4 — collisions
- **Theory:** point/segment vs sphere/capsule/plane, projection, friction, порядок constraints після колізії.
- **Resources:** Ericson; naelstrof `DoDepenetration`; VRM extended collider spec.
- **Task:** capsule на upper arms, plane грудної клітки, sphere-sphere L↔R.
- **Expected:** руки притискаються до тіла — груди деформуються/зсуваються без проникнення і без тремтіння.

### Level 5 — production architecture
- **Theory:** data-oriented дизайн, ScriptableObject профілі, реєстр рігів, lifecycle (pool/scene load), editor tooling, тести.
- **Resources:** розділ 9 і 20 цього документа.
- **Task:** `JiggleProfile`, `JiggleRig`, `JiggleWorld`, редакторський preview, пресети, PlayMode-тести edge cases (розділ 10).
- **Expected:** художник налаштовує без коду, всі edge cases покриті тестами.

### Level 6 — optimization
- **Theory:** Burst, Jobs, TransformAccessArray (паралелізм по root), LOD, culling, sleep, budget.
- **Resources:** Unity Jobs/Burst docs, Toca Boca стаття, naelstrof `JiggleJobs.cs`.
- **Task:** перенести simulate у Burst `IJobFor`, read/write через `IJobParallelForTransform`, 3 LOD-рівні, 100 персонажів у стрес-сцені.
- **Expected:** 100 персонажів × 4 кістки в межах бюджету, визначеного для платформи (напр. < 0.5 мс main thread на PC — **виміряти**, не вгадувати), 0 GC.

---

## 20. Recommended implementation architecture

### 20.1 Три варіанти

**A. Простий** (1–5 персонажів, прототип, dating sim)
- MonoBehaviour `SimpleJiggleBone` на кожній кістці, але оновлення з одного `JiggleUpdater` (щоб контролювати порядок).
- Позиційний спринг (f, ζ) semi-implicit Euler з фіксованими substeps, clamp, reset, maxAngle, без колізій.
- Trade-off: мало коду, легко зрозуміти; не масштабується, немає колізій, слабка інтеграція з неанімованими кістками (використати EZSoftBone-стиль revert у Update).

**B. Production** (рекомендовано)
- Verlet або спринг (f, ζ) на фіксованому кроці, гібрид world/local inertia, limits, proxy-колізії, reset/teleport, детект анімованості, профілі, LOD, опційно Burst.
- Trade-off: ~1.5–3k рядків коду, потребує тестів; покриває 95% ігор.

**C. Advanced** (hero-персонажі, AAA-якість, багато одягу)
- XPBD-солвер для ланцюгів + volume/shape-matching для грудей, двосторонні колізії з одягом, blendshape-корективи на основі симульованої деформації, ML/pose-space corrective shapes, інтеграція з Animation Rigging (IK рук, що торкаються тіла), GPU skinning-aware.
- Trade-off: дорого в розробці й CPU; оправдано лише для close-up/кат-сцен або коли груди/одяг мають взаємодіяти складно. Альтернатива — купити Magica Cloth 2 / Obi.

### 20.2 Production: структура класів (C#-ескіз, не копія бібліотеки)

```csharp
// ---------- Data ----------
[CreateAssetMenu(menuName = "Jiggle/Profile")]
public sealed class JiggleProfile : ScriptableObject
{
    [Range(0.5f, 8f)]  public float frequency = 2.5f;       // Hz
    [Range(0.05f, 1.5f)] public float dampingRatio = 0.45f; // zeta
    [Range(0f, 1f)]    public float gravityScale = 0.25f;
    [Range(0f, 1f)]    public float worldInertia = 0.6f;
    [Range(0f, 1f)]    public float localInertia = 0.8f;
    public float anchorSpeedLimit = 6f;          // m/s
    public float anchorAngularSpeedLimit = 720f; // deg/s
    [Range(0f, 60f)]   public float maxAngle = 15f;
    public float maxDisplacement = 0.025f;       // m at scale 1
    [Range(0f, 1f)]    public float translationWeight = 0.4f;
    [Range(0f, 0.5f)]  public float squashStretch = 0.1f;
    public Vector3 axisWeights = new Vector3(1f, 1.3f, 0.8f); // anchor-local anisotropy
    public float collisionRadius = 0.05f;
    public float maxParticleSpeed = 5f;          // m/s, safety clamp

    public event System.Action Changed;
    void OnValidate() => Changed?.Invoke();
}

public enum JiggleColliderType : byte { Sphere, Capsule, Plane }

public sealed class JiggleColliderProxy : MonoBehaviour
{
    public JiggleColliderType type;
    public float radius = 0.05f;
    public float height = 0.2f;   // capsule, along local Y
    public bool inside;           // keep particle inside sphere
}

// ---------- Authoring ----------
[System.Serializable]
public sealed class JiggleChainDefinition
{
    public Transform root;                 // Breast_L
    public Vector3 endOffsetLocal = new Vector3(0f, 0.12f, 0f); // tail if no child
    public JiggleProfile profile;
    public JiggleColliderProxy[] colliders;
    [Range(0f, 1f)] public float weight = 1f;
    public float frequencyJitter = 0.04f;  // L/R desync
}

[DisallowMultipleComponent]
public sealed class JiggleRig : MonoBehaviour
{
    public Transform anchorSpace;          // Chest / UpperChest (inertia reference)
    public JiggleChainDefinition[] chains;
    public float teleportDistance = 1.0f;  // m per frame
    public float teleportAngle = 90f;      // deg per frame
    public bool useUnscaledTime;

    internal int handle = -1;              // index in JiggleWorld
    void OnEnable()  { handle = JiggleWorld.Register(this); ResetSimulation(); }
    void OnDisable() { JiggleWorld.Unregister(this); handle = -1; }

    public void ResetSimulation()              => JiggleWorld.RequestReset(this, keepMotion: false);
    public void Teleport(bool keepMotion)      => JiggleWorld.RequestReset(this, keepMotion);
    public void SetWeight(float w, float time) => JiggleWorld.SetWeight(this, w, time);
}

// ---------- Simulation ----------
struct JiggleParticle            // SoA in NativeArrays in production
{
    public float3 pos, prevPos;          // world-space tail
    public float3 renderPos;             // interpolated output
    public quaternion restLocalRot;      // animated or bind pose this frame
    public float3 restLocalPos;
    public quaternion lastWrittenLocalRot;
    public float3 lastWrittenLocalPos;
    public float3 boneAxisLocal;         // from data (child/endOffset), mirror-safe
    public float boneLength;
    public int paramIndex, chainIndex, rigIndex;
}

struct JiggleParams              // baked from profile for fixed step h
{
    public float omega, zeta, h;
    public float3 gravity;               // Physics.gravity * gravityScale
    public float worldInertia, localInertia;
    public float maxAngleRad, maxDisp, maxStep, radius;
    public float3 axisWeights;
    public float translationWeight, squash, weight;
}

[DefaultExecutionOrder(10000)]
public sealed class JiggleWorld : MonoBehaviour { /* singleton, owns NativeArrays + TransformAccessArray */ }
```

### 20.3 Pseudocode кадру (JiggleWorld.LateUpdate)
```
const float H = 1f/90f;  MAX_STEPS = 4

LateUpdate():
  dt = useUnscaled ? Time.unscaledDeltaTime : Time.deltaTime
  foreach rig: UpdateLod(rig)                     // distance/visibility → Full/Reduced/Frozen

  // 1. CAPTURE (job: IJobParallelForTransform, read)
  foreach particle p (non-frozen):
      local = transform.localRotation/Position
      if local != p.lastWrittenLocal:  p.restLocal = local        // animated this frame
      else:                            /* not animated: keep previous restLocal (bind/anim) */
      compute target tail (world) from parent world matrix * restLocal * (boneAxis*len)
  foreach rig: read anchor world pose (current), keep previous
  foreach collider: read world matrix

  // 2. TELEPORT / DISCONTINUITY
  foreach rig:
      d = |anchorPos - anchorPosPrev|, a = angle(anchorRot, anchorRotPrev)
      if resetRequested or d > teleportDistance*scale or a > teleportAngle:
          if keepMotion: shift pos/prevPos by anchor delta transform (rigid)
          else:          pos = prevPos = target; accumulator = 0
          anchorPrev = anchor

  if dt <= 0: WritePose(lastRender); return        // pause / timeScale 0

  // 3. SIMULATE (Burst IJobFor over chains)
  accumulator += min(dt, MAX_STEPS*H)
  steps = floor(accumulator / H); accumulator -= steps*H
  for s in 0..steps-1:
      t = (s+1)/steps                                // sub-frame anchor interpolation
      anchor_s = Lerp/Slerp(anchorPrev, anchorNow, t); target_s = Lerp(targetPrev, targetNow, t)

      // inertia control: move history with the anchor (world vs local)
      anchorDelta = anchor_s.pos - anchor_{s-1}.pos
      clamp anchorDelta to anchorSpeedLimit*H; excess goes fully into history
      p.pos     += anchorDelta*(1-worldInertia) + excess
      p.prevPos += anchorDelta*(1-worldInertia) + excess
      (analogous rotation of history around anchor for angular part with localInertia/angular limit)

      // spring (Verlet form of x'' = -w^2 (x - target) - 2 z w v + g)
      v  = (p.pos - p.prevPos) / H
      a  = -w*w*(p.pos - target_s) - 2*z*w*v + gravity
      a  = anchorLocal( axisWeights * toAnchorLocal(a) )   // anisotropy
      x' = p.pos + v*H + a*H*H
      // velocity safety
      if |x' - p.pos| > maxParticleSpeed*H: clamp
      p.prevPos = p.pos; p.pos = x'

      // constraints
      disp = p.pos - target_s
      p.pos = target_s + SoftClamp(disp, maxDisp*scale)          // max displacement
      p.pos = ConeLimit(head, target_s, p.pos, maxAngle)          // max rotation
      // collisions
      foreach collider c in chain: p.pos = Depenetrate(p.pos, head, radius*scale, c)
      p.pos = ConeLimit(...)                                      // re-apply after collision
      if any NaN: p.pos = p.prevPos = target_s

  // 4. INTERPOLATE for render (alpha = accumulator/H between last two states)
  p.renderPos = Lerp(p.prevPos, p.pos, alpha)   // or keep 1-step history arrays

  // 5. WRITE (IJobParallelForTransform)
  foreach particle:
      animDir = normalize(target - head); simDir = normalize(renderPos - head)
      swing   = FromTo(animDir, simDir) scaled by weight*(1-translationWeight)
      localRot = inverse(parentWorldRot) * swing * parentWorldRot * restLocalRot
      localPos = restLocalPos + toParentLocal((renderPos - target) * translationWeight * weight)
      optional squash: scale along boneAxis = 1 + squash*dot(renderPos-target, animDir)/len
      transform.SetLocalPositionAndRotation(localPos, localRot)
      p.lastWrittenLocal = (localPos, localRot)
```
Примітки:
- Verlet з явним `-2ζω v` через `v = (x - x_prev)/H` — це по суті semi-implicit Euler у позиційній формі; стабільний при `ωH < ~1` з запасом (при f=5 Гц, H=1/90: ωH≈0.35) **[ENG]**. Якщо потрібно f>8 Гц — перейти на implicit (формула Chou) для спрингової частини.
- `restLocal` для неанімованих кісток = bind pose, збережений при реєстрації.
- Інтерполяція виходу додає ≤1 крок затримки (11 мс при 90 Гц) — непомітно.

### 20.4 Тест-план (мінімум)
1. EditMode: step response ζ/f відповідає аналітиці (Juckett/Holden) з похибкою < 2%.
2. PlayMode: однакова траєкторія tail при 30/60/144 FPS (фіксований сценарій руху якоря).
3. Teleport 1000 м, розворот 180° за кадр, timeScale 0→1, пауза 5 с, respawn з пулу, scene reload — displacement ніколи > maxDisp, немає NaN.
4. Scale 0.5 і 2.0 — однакова *відносна* амплітуда.
5. Неанімована кістка 10 хв idle — немає дрейфу.
6. Profiler: 0 B GC Alloc у LateUpdate; час на 1/10/50/100 персонажів.

---

## 21. Погані практики (tutorial-level, що ламаються в production)

| Практика | Чому ламається | Правильно |
|---|---|---|
| `transform.rotation = Quaternion.Lerp(current, target, 0.1f)` у Update | коефіцієнт на кадр → при 144 FPS в 2.4× "жорсткіше", ніж при 60; немає інерції/overshoot | спринг (f, ζ) на фіксованому кроці або `1 - exp(-λ dt)` |
| `Lerp(a, b, speed * Time.deltaTime)` | лише наближено FPS-незалежно; при великому dt `speed*dt>1` → overshoot/нестабільність | експоненційна форма або substeps |
| `v *= (1 - damping)` без dt | damping per frame (EZSoftBone, VRM drag) **[CODE]** | `v *= pow(1-d, dt/H_ref)` або фіксований крок |
| Фізика в `Update`/`FixedUpdate`, запис кісток там же | Animator перезаписує в своєму апдейті; FixedUpdate 0..N разів/кадр → jitter | LateUpdate + власний fixed-step accumulator |
| "Fixed" dt = 1/60 один раз на кадр | slow-mo при низькому FPS, fast-forward при високому (UnityChan) **[CODE]** | accumulator із реальним часом |
| SpringJoint на Rigidbody, прикріпленому до kinematic кістки, яку рухає Animator | PhysX не бачить швидкість kinematic, телепорт кожен кадр, розтяг, jitter **[SRC: RagdollStability]** | procedural bone solver |
| Немає reset | teleport/respawn/pool/scene load → вибух | Reset/Teleport API + автодетект |
| Немає max displacement / max angle / velocity clamp | спринт, root motion snap, hitch → розтяг "до колін" | 3 рівні запобіжників |
| Pure world-space симуляція | машини/ліфти/телепорти = величезні імпульси | world/local inertia + speed limits |
| Simulation у camera space / камера в ієрархії персонажа | рух камери трясе груди | world/anchor space |
| Модифікація кісток *до* Animator (у Update) і очікування, що збережеться | Animator перезапише анімовані кістки; неанімовані — дрейф | 6.3 фікс Б |
| `GetComponent`, `new List`, LINQ, `foreach` по Dictionary у LateUpdate | GC spikes | кеш, NativeArray/масиви, struct-дані |
| `LateUpdate` на кожній кістці (сотні MonoBehaviour) | overhead виклику, некерований порядок | один менеджер |
| Хардкод `Vector3.forward` як вісь кістки | mirrored/ротовані кістки поводяться по-різному | вісь з даних (child/endOffset) |
| `Quaternion.FromToRotation` для майже протилежних векторів без захисту | нестабільна вісь → ривки/NaN | normalizesafe + fallback axis (як у naelstrof **[CODE]**) |
| Ігнорування scale | маленькі/великі персонажі трясуться по-різному | масштабувати відстані lossyScale |
| Unity Cloth на тілі для грудей | немає об'єму, 16 колайдерів, погано керується **[SRC]** | bone jiggle |

---

## 22. Sources

### Unity official
- Execution order: https://docs.unity3d.com/6000.0/Documentation/Manual/execution-order.html
- AnimatorUpdateMode: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimatorUpdateMode.html
- Animator API: https://docs.unity3d.com/ScriptReference/Animator.html
- Avatar / humanoid: https://docs.unity3d.com/Manual/ConfiguringtheAvatar.html
- Animation Rigging DampedTransform: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/DampedTransform.html
- Animation Rigging manual/changelog: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/index.html
- Joint and Ragdoll stability: https://docs.unity3d.com/Manual/RagdollStability.html
- Rigidbody interpolation: https://docs.unity3d.com/Manual/rigidbody-interpolation.html
- ConfigurableJoint: https://docs.unity3d.com/6000.1/Documentation/ScriptReference/ConfigurableJoint.html
- Physics.SyncTransforms / autoSyncTransforms: https://docs.unity3d.com/ScriptReference/Physics.SyncTransforms.html , https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Physics-autoSyncTransforms.html , https://docs.unity3d.com/6000.2/Documentation/Manual/physics-optimization-cpu-transform-sync.html
- Cloth: https://docs.unity3d.com/2021.3/Documentation/Manual/class-Cloth.html
- IJobParallelForTransform / TransformAccessArray: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Jobs.IJobParallelForTransform.html , https://docs.unity3d.com/ScriptReference/Jobs.TransformAccessArray.html
- Unity Learn: https://learn.unity.com/course/prototyping-a-procedural-animated-boss/tutorial/using-animation-rigging-damped-transform , https://learn.unity.com/tutorial/working-with-animation-rigging?language=en
- NGO NetworkTransform: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/components/networktransform.html
- Unity Discussions (edge cases): https://discussions.unity.com/t/how-to-make-framerate-independent-rigging-constraint/1685301 , https://discussions.unity.com/t/jiggle-chain-constraint-as-presented-at-gdc2019/761949 , https://discussions.unity.com/t/cloth-component-collider-limit-of-16/653210

### Автори tools/frameworks
- naelstrof JigglePhysics: https://github.com/naelstrof/JigglePhysics (код: `Packages/com.gator-dragon-games.jigglephysics/Scripts/JiggleJobSimulate.cs`, `JigglePhysics.cs`, `JiggleJobInterpolation.cs`, `JiggleJobBulkTransformReset.cs`, `JiggleUpdateExample.cs`)
- naelstrof Blender Jiggle Physics: https://github.com/naelstrof/blender-jiggle-physics , https://extensions.blender.org/add-ons/jiggle-physics/
- UniVRM: https://github.com/vrm-c/UniVRM (код: `Packages/UniGLTF/Runtime/SpringBoneJobs/UpdateFastSpringBoneJob.cs`, `Packages/VRM/Runtime/SpringBone/VRMSpringBone.cs`, `.../Logic/SpringBoneJointState.cs`)
- VRMC_springBone spec: https://github.com/vrm-c/vrm-specification/blob/master/specification/VRMC_springBone-1.0/README.md ; extended collider: https://github.com/vrm-c/vrm-specification/blob/master/specification/VRMC_springBone_extended_collider-1.0/README.md ; issue: https://github.com/vrm-c/UniVRM/issues/2331
- UnityChanSpringBone: https://github.com/unity3d-jp/UnityChanSpringBone (код: `Runtime/SpringBone.cs`, `Runtime/SpringManager.cs`)
- EZSoftBone: https://github.com/EZhex1991/EZSoftBone (код: `Runtime/EZSoftBone.cs`)
- Automatic-DynamicBone: https://github.com/OneYoungMean/Automatic-DynamicBone
- SPCRJointDynamics: https://github.com/SPARK-inc/SPCRJointDynamics
- Magica Cloth 2: https://magicasoft.jp/en/magica-cloth-bone-spring-2/ , https://magicasoft.jp/en/magicaclothbonespringstart-2/ , https://magicasoft.jp/en/mc2_magicacloth_inertia/ , https://magicasoft.jp/en/mc2_baseline/ , https://magicasoft.jp/en/paramater-algorithm/ , https://assetstore.unity.com/packages/tools/physics/magica-cloth-2-242307
- Dynamic Bone: https://assetstore.unity.com/packages/tools/animation/dynamic-bone-16743 , http://willhongcom.blogspot.com/2014/04/dynamic-bone.html
- Boing Kit: https://assetstore.unity.com/packages/tools/particles-effects/boing-kit-dynamic-bouncy-bones-grass-and-more-135594 , http://longbunnylabs.com/boing-kit/
- Obi Softbody: https://assetstore.unity.com/packages/tools/physics/obi-softbody-130029
- VRChat PhysBones: https://creators.vrchat.com/common-components/physbones/
- Artell Spring Bones: https://github.com/artellblender/springbones
- Photon Fusion 2: https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop
- Mirror snapshot interpolation: https://mirror-networking.gitbook.io/docs/manual/components/network-transform/snapshot-interpolation

### GDC / SIGGRAPH / academic
- Jakobsen, Advanced Character Physics (GDC 2001): https://www.cs.cmu.edu/afs/cs/academic/class/15462-s13/www/lec_slides/Jakobsen.pdf
- Macklin, Müller, Chentanez, XPBD (MIG 2016): https://dl.acm.org/doi/pdf/10.1145/2994258.2994272
- Witkin & Baraff, Physically Based Modeling (SIGGRAPH 2001): https://graphics.pixar.com/pbm2001/
- Unity, Introducing the New Animation Rigging Features (GDC 2019): https://www.gdcvault.com/play/1026151/
- Unity, Animation Rigging workshop SIGGRAPH 2019: https://github.com/Unity-Technologies/animation-rigging-workshop-siggraph2019
- Scurr et al., breast displacement (J. Sports Sciences 2011): https://pubmed.ncbi.nlm.nih.gov/21077006/
- Ten Minute Physics: https://matthias-research.github.io/pages/tenMinutePhysics/index.html

### Технічні блоги
- Holden, Spring-It-On: https://theorangeduck.com/page/spring-roll-call
- Chou, Numeric Springing: https://allenchou.net/2015/04/game-math-precise-control-over-numeric-springing/ ; код: https://github.com/TheAllenChou/numeric-springing
- Juckett, Damped Springs: https://www.ryanjuckett.com/damped-springs/
- Fiedler, Fix Your Timestep: https://gafferongames.com/post/fix_your_timestep/
- Toca Boca, TransformAccessArray internals: https://medium.com/toca-boca-tech-blog/unitys-transformaccessarray-internals-and-best-practices-2923546e0b41

### Відео
- t3ssel8r: https://www.youtube.com/watch?v=KPoeNZZ6H4s
- Ten Minute Physics 09/10: https://www.youtube.com/watch?v=jrociOAYqxA , https://www.youtube.com/watch?v=uCaHXkS2cUg
- iHeartGameDev Animation Rigging: https://www.youtube.com/watch?v=Wx1s3CJ8NHw
- Magica Soft BoneCloth: https://www.youtube.com/watch?v=vNtJxBUgSaI
- Alex CGW, Jiggle Physics in Blender: https://www.youtube.com/watch?v=5HRzKJXpqfY
- UnityQueenGame, EZSoftBone setup: https://www.youtube.com/watch?v=eQMudj9VQ7Q

---

### Обмеження цього дослідження
- Закритий/платний код (Dynamic Bone, Magica Cloth 2, Boing Kit, Obi) не аналізувався — лише документація та сторінки магазину.
- Продуктивність бібліотек не бенчмаркалась; цифри "600 armadillos" — заява автора.
- Точні тривалості відео та дати частини ресурсів не вдалося надійно перевірити — позначено "—" або "~".
- Пресети в 11.2 та бюджети в 12.2 — інженерні стартові значення, не з джерел.
