# Unity Animator 2026: senior-level архітектура переходів, Layers і State Machines

Станом на **26 вересня 2026 року** актуальна документація Unity позначає **Unity 6.6 / 6000.6** як останній реліз. Нижче — не набір “постав Duration 0.1 і буде красиво”, а архітектура Animator Controller для production-проєкту: як не отримати павутину переходів, як розділяти логіку між Blend Trees, Sub-State Machines, Layers, кодом і Playables, та де саме у твоєму графі зараз закладені проблеми. citeturn13view0

Головна теза: **Animator повинен описувати композицію анімації, а не намагатися бути повною gameplay state machine**. Коли кожен рух напряму з'єднаний з кожним іншим рухом, ти вже не моделюєш анімацію — ти вручну програмуєш граф залежностей стрілочками. На маленькому прототипі це терпимо. На бойовій системі з cancels, wall movement, traversal, damage reactions і різною зброєю це перетворюється на архітектурний борг.

## Що не так із поточним графом

На твоєму скріні видно типову стадію **Animator spaghetti**: `Locomotion`, `Flip`, `Wall Jump`, `Wall Run`, `Jump Start`, `Airborne`, `Land`, `Roll`, `Slide Start`, `Slide Loop`, `Slide Exit`, `Vault`, `Dash`, `Stumble`, `Attack`, `Attack2` живуть приблизно на одному рівні й зв'язані великою кількістю переходів.

![Поточний Animator Controller](sandbox:/mnt/data/629b5448-8e8a-429e-801c-8e2b3b1a9b55.png)

Сам факт великої кількості states не є проблемою. Проблема — **топологія**. Unity прямо дає Sub-State Machines і State Machine Transitions саме для спрощення великих state machines і підняття логіки на вищий рівень абстракції. Sub-State Machine дозволяє згортати пов'язану групу states в один вузол, а Entry/Exit — керувати входом та виходом із цієї групи. citeturn15view6turn15view7

У твоєму випадку states змішують щонайменше п'ять різних понять:

| Те, що видно зараз | Чим це насправді є | Де має жити |
|---|---|---|
| Idle / Walk / Run / Strafe | безперервна locomotion | `Blend Tree` |
| Jump Start / Airborne / Land | багатофазний airborne mode | `Airborne` Sub-State Machine |
| Wall Run / Wall Jump / Vault | traversal family | `Traversal` Sub-State Machine |
| Slide Start / Loop / Exit | одна складна дія з фазами | `Slide` Sub-State Machine |
| Roll / Dash / Flip | full-body actions | `FullBodyActions` SM або окремі action states |
| Attack / Attack2 | combat action | upper-body Layer або full-body Combat SM |
| Stumble / hit / death | reaction / interrupt | reaction state/SM, іноді `Any State` |

Це **архітектурна рекомендація**, а не обмеження Unity. Але вона прямо випливає з призначення інструментів: Unity рекомендує Blend Trees для змішування схожих рухів, Sub-State Machines — для багатоступеневих складних дій, а Layers — для незалежної анімації різних частин тіла. citeturn14view4turn15view6turn14view2

Тобто верхній рівень Base Layer я б приводив приблизно до такого виду:

```text
Base Layer
│
├── Locomotion
│   └── Blend Tree
│       ├── Idle
│       ├── Walk
│       ├── Run
│       └── Strafe / directional movement
│
├── Airborne
│   ├── Entry
│   ├── JumpStart
│   ├── Falling / Airborne
│   └── Land
│
├── Traversal
│   ├── WallRun
│   ├── WallJump
│   └── Vault
│
├── Slide
│   ├── SlideStart
│   ├── SlideLoop
│   └── SlideExit
│
├── FullBodyActions
│   ├── Roll
│   ├── Dash
│   └── Flip
│
└── FullBodyReaction
    └── Stumble
```

А поверх цього:

```text
Layer 0 — Base
    Full body / locomotion / traversal

Layer 1 — UpperBody_Action
    Override + upper-body Avatar Mask
    Attack / reload / interact / weapon actions

Layer 2 — UpperBody_Additive
    Additive + upper-body mask
    Recoil / aim offset / breathing / small poses

Layer 3 — Optional procedural/reaction layer
    тільки якщо реакція дійсно незалежна від Base
```

Тут принцип важливіший за конкретні назви: **Sub-State Machine групує поведінку; Layer компонує одночасні анімації.** Використовувати Layer просто як папку для `WallRun`, бо Animator став некрасивим, — неправильна абстракція.

## Як професійно будувати transitions

Unity Transition має чотири критичні групи налаштувань: **умови**, **Exit Time**, **blend duration/offset** і **interruption policy**. Перехід визначає як момент зміни state, так і тривалість blending між source та destination. citeturn14view0turn17view1

### Has Exit Time — не “плавніше”, а семантика дозволу на вихід

`Has Exit Time` прив'язує можливість переходу до normalized time source animation. `Exit Time = 0.75` означає вікно на моменті приблизно 75% кліпу; для loop-анімації значення нижче `1` перевіряється кожен цикл, а значення вище `1` може використовуватися для виходу після заданої кількості loops. `Fixed Duration` перемикає Duration між секундами та normalized часткою source state. citeturn17view1

Практично це дає два принципово різні типи переходів.

**Responsive transition** — gameplay уже вирішив, що дія має відбутися:

```text
Locomotion -> Jump
Locomotion -> Roll
Locomotion -> Dash
Airborne -> WallRun
Anything allowed -> HitReact
```

Для них зазвичай:

```text
Has Exit Time = OFF
Condition = gameplay parameter / trigger
Transition Duration = короткий
```

Ти не хочеш, щоб гравець натиснув jump, а персонаж подумав: “секундочку, я ще не дограв 82% run cycle”. Це вже не animation polish, а input lag у костюмі аніматора.

**Completion transition** — state має природно дійти до певної фази:

```text
JumpStart -> Airborne
Land -> Locomotion
Roll -> Locomotion
SlideStart -> SlideLoop
SlideExit -> Locomotion
AttackRecover -> locomotion
```

Тут `Has Exit Time` часто доречний, бо вихід пов'язаний із прогресом кліпу. Unity також дозволяє одночасно використовувати Exit Time та Conditions; в такому випадку parameter conditions починають мати значення після досягнення Exit Time. citeturn17view1

Стартові tuning-діапазони, **не правила Unity**:

| Тип | Has Exit Time | Blend, орієнтир | Сенс |
|---|---:|---:|---|
| Locomotion → Jump | Off | 0.03–0.10 s | responsiveness |
| Locomotion → Roll/Dash | Off | 0.03–0.08 s | action interrupt |
| JumpStart → Airborne | On або animation phase | 0.03–0.10 s | завершити anticipation |
| Land → Locomotion | On/conditional | 0.05–0.15 s | не знищити impact pose |
| One-shot → Locomotion | On | 0.05–0.15 s | clean recovery |
| Stumble / hard hit | Off | 0.02–0.08 s | high-priority interrupt |
| Death / knockdown | Off | майже миттєвий | terminal/high-priority state |

Числа потрібно тюнити по конкретних clips, швидкості gameplay і camera distance. “У всіх transition duration 0.25” — це не convention, це капітуляція.

### Fixed Duration проти normalized duration

При `Fixed Duration = On` Unity трактує Duration у секундах; при `Off` — як normalized fraction від source state. citeturn17view1

Для **коротких responsive actions** я частіше використовую fixed seconds, бо тоді 0.06 с означає 0.06 с незалежно від довжини source clip.

Для систем, де різні clips можуть бути різної довжини, normalized duration іноді краще масштабується. Особливо це важливо при clip overriding: Unity окремо попереджає, що при Animator Override Controller тривалість кліпів може відрізнятися, тому normalized exit timing краще переноситься між варіантами. citeturn15view15

### Transition Offset — не декоративний параметр

`Transition Offset` визначає, з якої normalized позиції почне програватися destination. Тобто можна увійти не з `0%`, а, наприклад, із `50%` target clip. citeturn17view1

Для cyclic locomotion це корисно при phase matching. Наприклад, якщо source state залишає персонажа з лівою ногою вперед, бездумний crossfade в target clip із правою ногою вперед може створити ковзання або дивну зміну опори.

Unity прямо рекомендує для Blend Trees схожі motions з узгодженими timing landmarks; ходьба і біг повинні бути синхронізовані так, щоб ключові події циклу на кшталт контакту стопи припадали приблизно на однаковий normalized time. citeturn13view4

### Interruption Source — місце, де production Animator або працює, або починає брехати

У Unity в один момент активний один transition на layer, але він може бути перерваний іншим transition залежно від `Interruption Source` та `Ordered Interruption`. Unity концептуально формує queue кандидатів; **Any State transitions потрапляють у цю чергу першими**, а потім transitions поточного та/або наступного state залежно від `Interruption Source`. citeturn14view1

Можливості:

```text
None
Current State
Next State
Current State then Next State
Next State then Current State
```

А `Ordered Interruption` впливає на те, коли Unity припиняє пошук у цій черзі, тобто фактично transition ordering стає частиною priority system. citeturn15view1

Це означає: **порядок transitions не повинен бути випадковим**.

Для action game логічний conceptual priority може бути таким:

```text
Death
  >
Knockdown / Hard Stun
  >
Hit Reaction
  >
Dodge / Emergency Cancel
  >
Attack / Ability
  >
Traversal
  >
Locomotion
```

Це вже не правило engine, а design policy. Але policy має існувати. Інакше ти отримаєш баги класу “attack sometimes cancels roll only when previous transition already started” і будеш три години дивитися на сині стрілки, як археолог на шумерську табличку.

Unity окремо пояснювала mechanics interruption: transition priority та source/next-state ordering визначають, який кандидат переможе, коли кілька transitions стають valid. citeturn14view13turn15view18

## Layers: де вони потрібні, а де роблять гірше

Animation Layer — це **окрема state machine, результат якої компонується з попередніми layers**. На кожному layer можна визначити Avatar Mask і blending mode. `Override` замінює результат нижніх layers для контрольованих properties/body parts; `Additive` додає результат поверх нижніх layers. Unity прямо наводить типовий приклад: нижня частина тіла виконує locomotion, верхня — shooting/throwing. citeturn14view2turn15view2

Тому Layers треба вводити не за принципом:

> “states стало багато, винесу половину на Layer 2”.

А за принципом:

> “ці дві анімаційні системи повинні працювати **одночасно й незалежно**”.

### Base Layer

На Base я б залишив те, що визначає фундаментальну full-body pose:

```text
Locomotion
Jump / Fall / Land
Wall movement
Vault
Slide
Roll
Dash
Flip
Full-body stumble / knockdown
```

Особливо якщо рух зачіпає root/pelvis/legs або має Root Motion. Якщо `Vault` реально переносить тіло через перешкоду, ховати його в upper-body layer — це вже сюрреалізм.

### UpperBody Override

Тут ідеальні кандидати:

```text
Attack
Shoot
Reload
Use Item
Interact руками
Weapon equip
Upper-body cast
```

але тільки якщо нижня частина тіла при цьому має продовжувати locomotion.

```text
Base:
Run Forward

UpperBody:
SwordAttack

Result:
Legs = Run
Spine/Arms = Attack
```

Для цього layer отримує **Avatar Mask**, що включає потрібний spine/chest/arms/head і виключає legs/root. Unity Avatar Mask може маскувати Humanoid body parts або конкретні transforms у hierarchy; також можна окремо включати/виключати IK curves. citeturn14view3turn16view0

Якщо Attack повинен зупинити персонажа, повернути pelvis, зробити full-body anticipation або керувати root motion — він уже не upper-body overlay. Він має йти в Base/full-body combat flow.

### Additive Layer

Additive layer годиться для **дельт**, а не для альтернативної повної пози:

```text
Aim pitch/yaw offset
Recoil
Breathing
Weapon sway
Small flinch
Leaning
Pose correction
```

Unity зазначає, що Additive додає animation поверх попереднього layer і для коректної роботи відповідні animated properties мають збігатися з тими, на які накладається результат. citeturn15view2

Тому звичайний full-body attack clip не треба кидати в Additive і чекати магії. Additive clip має бути підготовлений як additive motion/reference-relative delta. Інакше замість “professional animation stack” можна отримати персонажа, який намагається скласти власний хребет у четвертий вимір.

### Avatar Masks дають не лише порядок, а й економію

Unity рекомендує маскувати непотрібні Humanoid IK goals або finger animation, якщо вони не використовуються. Маскування також дозволяє не обробляти непотрібні частини animation data. citeturn17view2

Практично: якщо upper-body attack взагалі не повинен впливати на ноги, не сподівайся, що “кліп там наче нічого не робить”. Маска робить контракт явним.

### Layer Sync — спеціалізований інструмент

Unity дозволяє синхронізувати layer з іншим layer: вони використовують **ту саму структуру state machine**, але можуть мати інші animation clips. Типовий офіційний приклад — healthy/wounded variants walk/run/jump. Synced layer не має незалежної структури; зміни структури відбиваються на source layer. citeturn17view4

Це доречно для:

```text
Normal locomotion
└── wounded animation set

Normal locomotion
└── exhausted animation set
```

Але це не заміна Sub-State Machines.

## Blend Trees, Sub-State Machines та Any State

Це три інструменти, які найчастіше використовують не за призначенням.

### Blend Tree: одна семантична дія, багато варіантів

Unity чітко розділяє поняття: **Transition** переводить state machine з одного стану в інший; **Blend Tree** одночасно змішує схожі motions відповідно до параметрів. Walk/run залежно від speed — канонічний Blend Tree use case. citeturn15view4turn16view3

Отже:

```text
Idle -> Walk -> Run
```

як три states із transition arrows — часто зайва складність.

Краще:

```text
Locomotion
└── 1D Blend Tree: Speed
    0.0 = Idle
    1.5 = Walk
    4.5 = Run
```

Якщо є directional locomotion:

```text
Locomotion
└── 2D Blend Tree
    MoveX
    MoveY
```

Unity має кілька 2D моделей, і вибір реально важливий. citeturn16view4

| Blend Type | Коли |
|---|---|
| **2D Simple Directional** | Forward / Back / Left / Right, один motion на напрямок |
| **2D Freeform Directional** | у тому самому напрямку є, наприклад, Walk Forward і Run Forward |
| **2D Freeform Cartesian** | X і Y означають різні фізичні величини, наприклад linear speed + angular speed |
| **Direct** | код або animation system напряму задає weight кожного child; корисно для facial/random blending |

Unity прямо зазначає, що `Simple Directional` не розрахований на кілька motions в одному напрямку, тоді як `Freeform Directional` це підтримує і очікує center motion на `(0,0)`. `Freeform Cartesian` доречний, коли осі не є просто напрямками. citeturn16view4turn15view5

### Sub-State Machine: одна поведінка, багато фаз

`Slide Start`, `Slide Loop`, `Slide Exit` — майже підручниковий Sub-State Machine:

```text
Slide
│
Entry
  ↓
Start
  ↓
Loop
  ↓
Exit
  ↓
Exit Node
```

На верхньому рівні Base Layer тобі не потрібно бачити три states і шість стрілок. Має бути **одна концепція `Slide`**.

Так само:

```text
Airborne
│
├── JumpStart
├── Falling
└── Land
```

Причому Entry transitions можуть вибирати стартовий state залежно від parameters. Наприклад:

```text
Base -> Airborne

Airborne.Entry
├── JumpStart    if JumpInitiated
└── Falling      if !JumpInitiated
```

Тобто якщо гравець натиснув jump — отримує anticipation `JumpStart`; якщо просто зійшов із уступу — одразу `Falling`. Unity Entry node саме для цього й може branching-ом вибирати destination state за conditions. citeturn15view7

Це значно чистіше, ніж мати:

```text
Locomotion -> JumpStart
Locomotion -> Airborne
WallRun -> Airborne
Vault -> Airborne
Dash -> Airborne
...
```

### Any State: глобальний interrupt, а не “мені лінь провести стрілки”

Unity визначає `Any State` як спосіб зробити один і той самий transition із **будь-якого state**; офіційний приклад — глобальна подія, яка має спрацювати незалежно від поточного state. citeturn17view0

Звідси senior-level правило:

**Any State використовуй лише тоді, коли семантика дійсно означає “це може початися майже звідусіль”.**

Хороші кандидати:

```text
Death
Hard Hit Reaction
Knockdown
Global Stun
Emergency cinematic override
```

Залежно від дизайну:

```text
Dodge
Roll
```

Погані кандидати:

```text
Any State -> Land
Any State -> JumpStart
Any State -> Attack1
Any State -> Attack2
Any State -> SlideExit
```

якщо ці дії насправді доступні лише з конкретних gameplay modes.

Причина не лише в чистоті graph. Any State transitions першими входять у interruption queue, тому їх множення буквально ускладнює priority semantics. citeturn14view1

Якщо `Attack` дозволений тільки з locomotion та деяких recovery states, це не `Any State`. Це **правило combat state machine**.

## Gameplay state і animation state не повинні бути одним і тим самим

Animator Parameters — це змінні Animator Controller, через які script впливає на state-machine flow. Unity підтримує `Float`, `Int`, `Bool` і `Trigger`; `Trigger` автоматично скидається controller після того, як був спожитий transition. citeturn14view9turn16view6

Ключове слово тут: **впливає**.

Не треба будувати всю game logic так:

```csharp
if (animator.GetCurrentAnimatorStateInfo(0).IsName("Roll"))
{
    playerCanMove = false;
}
```

Це перевертає ownership догори ногами. Тоді animation graph починає визначати combat rules, input windows, movement authority і physics. Змінив animator clip — поламав gameplay.

Краще:

```text
Input
  ↓
Gameplay State / Ability / Character Motor
  ↓
Рішення:
"ми зараз реально починаємо Roll"
  ↓
Animator presentation
```

Animator має знати:

```text
Speed
MoveX
MoveY
VerticalSpeed
Grounded
Action/ActionTrigger
TraversalMode
```

а не зберігати 27 взаємовиключних boolean:

```text
IsIdle
IsWalking
IsRunning
IsJumping
IsFalling
IsLanding
IsRolling
IsDashing
IsSliding
IsVaulting
...
```

Бо тоді цілком можливо отримати:

```text
IsGrounded = true
IsFalling = true
IsJumping = true
IsSliding = true
```

і Animator чесно спробує виконати весь цей абсурд згідно з transition ordering.

### Параметри мають описувати signal, а не дублювати кожен state

Наприклад:

```csharp
public static class AnimHashes
{
    public static readonly int Speed =
        Animator.StringToHash("Speed");

    public static readonly int MoveX =
        Animator.StringToHash("MoveX");

    public static readonly int MoveY =
        Animator.StringToHash("MoveY");

    public static readonly int VerticalSpeed =
        Animator.StringToHash("VerticalSpeed");

    public static readonly int Grounded =
        Animator.StringToHash("Grounded");

    public static readonly int Roll =
        Animator.StringToHash("Base Layer.FullBodyActions.Roll");
}
```

Unity рекомендує hashes замість повторного string lookup для Animator queries, а `Animator.Play`/`CrossFade` API також підтримують precomputed state hashes. citeturn17view3turn15view14

Для locomotion parameter smoothing можна подавати фактичну gameplay velocity і дампити input у Animator, замість плодити transitions між walk/run clips.

Концептуально:

```csharp
animator.SetFloat(SpeedHash, speed);
animator.SetFloat(MoveXHash, localVelocity.x);
animator.SetFloat(MoveYHash, localVelocity.z);
animator.SetFloat(VerticalSpeedHash, velocity.y);
animator.SetBool(GroundedHash, isGrounded);
```

### Не кожна gameplay-команда потребує transition arrow

Unity має `Animator.CrossFade`, який може перейти з поточного state безпосередньо в будь-який інший state із normalized transition duration, і `CrossFadeInFixedTime`, де transition duration задається в секундах. `Animator.Play` може напряму відтворити state. citeturn16view7turn15view13turn15view14

Це дуже корисно, коли **gameplay code уже вирішив**, що дія валідна.

Замість:

```text
Locomotion ------\
Airborne ---------\
WallRun -----------\
Slide --------------> Roll
AttackRecover -----/
TraversalExit -----/
```

можна мати gameplay action resolver:

```csharp
public void PlayRoll()
{
    animator.CrossFadeInFixedTime(
        AnimHashes.Roll,
        fixedTransitionDuration: 0.06f,
        layer: 0);
}
```

`CrossFadeInFixedTime` використовує секунди, тоді як `CrossFade` — normalized transition duration. citeturn15view12turn15view13

Це не означає “видалити всі transitions і керувати Animator із коду”. Це інша крайність.

Хороший hybrid:

```text
Transitions:
- locomotion structural flow
- airborne phases
- animation-driven completion
- artist-tunable state flow

Direct CrossFade:
- gameplay-resolved action requests
- attacks
- abilities
- dodge/roll
- context-sensitive actions
```

Тоді у графі зникає значна частина N-to-N зв'язків.

### StateMachineBehaviour — glue, а не другий GameManager

Unity дозволяє додавати `StateMachineBehaviour` до state та отримувати callbacks під час enter/update/exit; офіційні use cases включають sounds, context-specific checks і VFX. citeturn15view10

Добре:

```text
OnStateEnter:
- запустити trail
- повідомити animation presentation layer
- увімкнути VFX

OnStateExit:
- вимкнути trail
- cleanup visual state
```

Обережно:

```text
OnStateEnter:
- списати mana
- застосувати damage
- вирішити, чи персонаж взагалі мав право атакувати
- переключити authoritative network state
```

Авторитетна gameplay-логіка, особливо combat/networking, не повинна випадково залежати від того, чи animator callback відпрацював саме цього frame. Це вже архітектурний висновок, не обмеження API.

### Write Defaults

`Write Defaults` визначає, чи state записує default values для properties, які його motion **не анімує**. citeturn16view5

Тому це не checkbox категорії “ніколи не чіпай”. Він прямо змінює поведінку неанімованих properties, що особливо важливо в layered/partial-animation setup.

Senior policy тут проста: **визначити для проєкту одну зрозумілу convention і не змішувати режими хаотично без розуміння наслідків**. Особливо перевіряти states, що анімують лише subset properties, additive/override layers та clips із material/blendshape/custom-property curves.

## Масштабування: Override Controllers, Playables і performance

Великий Animator Controller не обов'язково повільний. Павутина на скріні передусім є проблемою **reasoning, testing і maintainability**, а не доказом FPS bottleneck.

Unity пише, що overhead самого layer зазвичай невеликий, а фактична вартість залежить від animations та Blend Trees, які він оцінює. Якщо layer має `weight = 0`, Unity пропускає його update. citeturn17view2

Отже не треба з релігійним страхом уникати трьох layers. Потрібно уникати **безглуздих layers**, які постійно обчислюють купу непотрібних animation data.

Із актуальних рекомендацій Unity:

- scale curves дорожчі за translation/rotation curves;
- для Humanoid варто маскувати непотрібні IK/finger animation;
- використовувати hashes замість repeated strings;
- для невидимих персонажів Unity рекомендує `Animator Culling Mode = Cull Completely` і вимикати `SkinnedMeshRenderer.Update When Offscreen`, коли це допустимо для конкретної гри;
- layer із zero weight не оновлюється. citeturn17view2turn17view3

### Animator Override Controller — коли логіка та сама, clips різні

Якщо у тебе є:

```text
Sword:
Attack1_Sword
Attack2_Sword
DashAttack_Sword

Axe:
Attack1_Axe
Attack2_Axe
DashAttack_Axe

Hammer:
Attack1_Hammer
Attack2_Hammer
DashAttack_Hammer
```

не обов'язково робити три копії state machine.

`AnimatorOverrideController` зберігає структуру, parameters і logic вихідного Animator Controller, але підміняє clips. Unity саме для цього його і призначає. citeturn15view15

Тобто:

```text
Base Combat Controller
    Attack1
    Attack2
    DashAttack

Sword Override
    ↓ clips

Axe Override
    ↓ clips

Hammer Override
    ↓ clips
```

При runtime swap Override Controller, який базується на тому самому Animator Controller, поточний state machine state не скидається. Але Unity попереджає, що зміна окремих overrides через indexer може викликати reallocation clip bindings на кожному виклику, тому масові заміни треба робити відповідним batch-підходом (`ApplyOverrides`). citeturn16view8

### Коли Animator Controller уже не той інструмент

Unity Playables API дозволяє:

- dynamically blend animations;
- програвати clip без окремого Animator Controller asset;
- будувати blend graphs у runtime;
- керувати weights;
- додавати/змінювати graph nodes динамічно замість створення величезного static graph, що передбачає всі можливі outcomes. citeturn14view12turn15view17

Це важлива межа.

Припустимо, гра має:

```text
30 weapons
10 attacks per weapon
contextual finishers
different injuries
procedural aim
upper-body weapon poses
interaction clips
runtime buffs/debuff poses
```

Спроба зашити весь combinatorial space в Mecanim стрілками — це не “використовуємо engine по максимуму”. Це розробка Excel у Animator Window.

Тут доречна архітектура:

```text
Animator Controller
    stable core:
    locomotion / airborne / base traversal

Playables
    dynamic:
    contextual clips
    weapon/action blending
    temporary runtime graphs
    procedural composition
```

Playables не треба вводити лише тому, що вони звучать senior. Вони мають сенс, коли **динамічна композиція** реально простіша за static controller graph. Unity прямо позиціонує їх як доповнення до Mecanim, а не як обов'язкову заміну. citeturn14view12

## Як я б перебудував саме твій Animator

Твій current controller можна рефакторити без повного переписування за один раз.

### Base Layer після рефакторингу

```text
                         ┌──────────────┐
                         │  Locomotion  │
                         │  Blend Tree  │
                         └──────┬───────┘
                                │
               ┌────────────────┼────────────────┐
               │                │                │
               ▼                ▼                ▼
        ┌────────────┐    ┌────────────┐   ┌──────────────┐
        │ Airborne   │    │ Traversal  │   │ FullBodyAct  │
        │    SM      │    │    SM      │   │     SM       │
        └─────┬──────┘    └──────┬─────┘   └──────┬───────┘
              │                  │                │
              └──────────────────┴────────────────┘
                                │
                                ▼
                         ┌──────────────┐
                         │  Locomotion  │
                         └──────────────┘
```

Не потрібно буквально робити всі двосторонні arrows між усіма трьома machines. Gameplay state повинен вирішувати, чи може `Traversal -> Roll`, `Airborne -> Dash` тощо. Якщо це точкові gameplay actions, direct CrossFade часто чистіший за десятки edge transitions. Unity підтримує переходи між states/state machines та напряму через `CrossFade`, тому тут обидва механізми легітимні — питання архітектури. citeturn15view7turn16view7

### Locomotion

Зараз `Locomotion` у тебе вже виглядає як один state, що добре. Усередині нього варто тримати саме Blend Tree:

```text
Speed + MoveDirection

Idle
WalkForward
WalkBack
StrafeLeft
StrafeRight
RunForward
RunStrafe...
```

Якщо рух вільно-directional і має walk/run в одному напрямку — `2D Freeform Directional` природніший за `Simple Directional`, бо Unity саме цей тип передбачає для кількох motions одного напрямку. citeturn16view4

### Jump Start + Airborne + Land

В один `AirborneSM`.

```text
Entry
├── JumpStart ──────> Falling
└── Falling

Falling ────────────> Land
Land ───────────────> Exit
```

Conditions:

```text
Entry -> JumpStart:
    JumpInitiated

Entry -> Falling:
    default

Falling -> Land:
    Grounded == true

Land -> Exit:
    Has Exit Time
```

Тут `Grounded` має приходити з character motor/ground detection, а не визначатися через Animator. Animator отримує факт і малює його.

### Slide Start + Slide Loop + Slide Exit

В один `SlideSM`.

```text
Entry
  ↓
SlideStart
  ↓
SlideLoop
  ↓ when !Sliding
SlideExit
  ↓
Exit
```

Тобі більше не треба бачити три states на root graph.

### Wall Run + Wall Jump + Vault

У `TraversalSM`, але не тому, що вони всі “акробатика”. А тому, що їх об'єднує **contextual traversal ownership**.

```text
TraversalMode
0 = None
1 = WallRun
2 = WallJump
3 = Vault
```

Entry може branch-итися залежно від `TraversalMode`, що є прямим use case Entry transitions. citeturn15view7

При більш серйозній traversal system я б узагалі тримав detection/alignment/target selection у коді, а Animator використовував тільки для selected motion.

### Roll + Dash + Flip

Тут рішення залежить від gameplay semantics.

Якщо всі три:

```text
full body
short-lived
block/modify locomotion
можуть бути requested gameplay-кодом
```

то:

```text
FullBodyActionsSM
├── Roll
├── Dash
└── Flip
```

або навіть окремі states, що запускаються через `CrossFadeInFixedTime`.

Не треба робити:

```text
Every state -> Roll
Every state -> Dash
Every state -> Flip
```

коли валідність action уже знає gameplay state machine.

### Attack і Attack2

Ось тут треба розділити два випадки.

**Можна атакувати під час бігу:**

```text
Base Layer:
Locomotion

UpperBody_Action Layer:
Empty
Attack1
Attack2

Avatar Mask:
Spine + Chest + Arms (+ Head за потреби)

Blend:
Override
```

Unity Layers і Avatar Masks прямо розраховані на сценарій, де нижня частина тіла продовжує locomotion, а верхня виконує незалежну action animation. citeturn16view2

**Атака full-body / root-motion / movement-lock:**

```text
Base Layer
└── CombatActionSM
    ├── Attack1
    └── Attack2
```

Не намагайся “врятувати” locomotion Layer-ом, якщо design самої атаки вимагає контролю всього тіла.

### Stumble

Якщо це сильна full-body реакція:

```text
Any State -> Stumble
```

може бути цілком правильно — **за умови**, що Stumble справді дозволено перебити майже все.

Тут:

```text
Has Exit Time = OFF
Interruption priority = висока
Can transition to self = за design
Recovery -> відповідний gameplay mode
```

Але death/knockdown, якщо вони є, повинні мати вищий semantic priority. Оскільки Any State transitions стоять попереду в interruption queue, їх порядок та conditions треба проектувати як priority system, а не складати туди все підряд. citeturn14view1turn17view0

### Фінальна схема відповідальності

Найкорисніша mental model для production Animator:

| Проблема | Інструмент |
|---|---|
| Idle ↔ Walk ↔ Run | **Blend Tree** |
| 8-direction movement | **2D Blend Tree** |
| JumpStart → Airborne → Land | **Sub-State Machine** |
| SlideStart → Loop → Exit | **Sub-State Machine** |
| WallRun / WallJump / Vault family | **Sub-State Machine** |
| Upper-body attack while running | **Override Layer + Avatar Mask** |
| Recoil / aim correction | **Additive Layer** |
| Death / universal hard interrupt | **Any State** |
| Gameplay already selected Roll/Dash/Attack | **CrossFade / CrossFadeInFixedTime** |
| Same logic, different character/weapon clips | **AnimatorOverrideController** |
| Runtime combinatorial animation system | **Playables API** |
| VFX/audio/state presentation callbacks | **StateMachineBehaviour** |
| Input buffering / cancels / gameplay permissions | **Gameplay code/state machine** |

Unity вже має окремі механізми для кожного з цих рівнів: Blend Trees для схожих motions, Sub-State Machines для complex staged actions, Layers для simultaneous body-part composition, Any State для global transitions, Override Controllers для clip variants і Playables для runtime dynamic graphs. Намагатися вирішити все одними transition arrows — це приблизно як використовувати `Update()` як dependency injection container: технічно компілюється, але питання вже не до компілятора. citeturn16view3turn15view6turn15view2turn17view0turn15view15turn15view17

**Цільова ознака хорошого Animator Controller:** дивлячись лише на root graph, за кілька секунд видно великі animation modes персонажа; заходячи в конкретний Sub-State Machine, видно тільки його локальний flow; Layers означають незалежні композиційні канали тіла; а правила типу “чи можна скасувати Attack2 у Roll на 37% recovery” живуть у gameplay/combat architecture, а не заховані в дев'ятій стрілці Inspector-а.