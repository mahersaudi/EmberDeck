EmberDeck — نسخة تجريبية 0.1.0 (ماك)
=====================================

شكرًا إنك بتجرب اللعبة! هذي نسخة تجريبية، ورأيك الصريح هو أهم شي.


الجديد في هذه النسخة
--------------------
- الفصل الثالث «قلب الجمر»: سبعة أعداء جدد وزعيم أخير. الجولة الكاملة صارت ثلاثة فصول.
- قبل كل جولة تبني مجموعة من 30 بطاقة. زر «مجموعات» فيه مجموعات جاهزة (المحرقة، السندان، الشرر،
  فرط الحرارة) وخانات تحفظ فيها مجموعاتك.
- قانون «نار القلب» في الفصل الثالث: تبدأ دورك وحرارتك فوق الحد فتأخذ طاقة إضافية، لكنها تحرقك.
- معارك الممرات تعطي ذهبًا بدل بطاقة؛ البطاقات من النخبة والزعماء والكنوز والمتجر.
- تقدر تسحب البطاقة على العدو، والضربات صار لها تأثير واهتزاز (يُطفأ من الإعدادات).
- صحتك تبدأ 50 والأعداء أُعيد ضبطهم — أخبرنا إذا صارت اللعبة أسهل أو أصعب من اللازم.

1) كيف تفتح اللعبة
------------------
- فك الضغط عن الملف، وبيطلع لك EmberDeck.app (تقدر تنقله لمجلد Applications).
- تشتغل على أجهزة ماك بمعالج Intel وبمعالج Apple (M1 وما بعده).
- أول مرة تفتحها، الماك بيقول إنه ما يقدر يتحقق من المطوّر، لأن النسخة غير موقّعة من Apple.
  الحل:
    • اضغط على اللعبة بالزر اليمين ← «فتح» ← «فتح».
    • إذا ما نفع (في macOS الجديد): افتح «إعدادات النظام» ← «الخصوصية والأمان»،
      انزل تحت وبتلقى رسالة عن EmberDeck ← اضغط «فتح على أي حال».
    • أو من تطبيق Terminal اكتب هذا الأمر (بعد ما تنقل اللعبة لمجلد Applications):
        xattr -dr com.apple.quarantine /Applications/EmberDeck.app


2) قبل ما تبدأ
--------------
- اللعبة بالعربي والإنجليزي: من «Settings» ← «Language» غيّر اللغة، وتتطبق لما تطلع من الشاشة.
- تقدر تلعب بالماوس، أو الكيبورد (الأسهم + Enter + Esc)، أو يد تحكم.
- «جولة جديدة» تفتح شاشة بناء المجموعة: تختار 30 بطاقة قبل ما تبدأ، وزر «مقترحة» يعبّيها لك
  إذا ما تبغى تبني بنفسك. البطاقة النادرة عدد نسخها أقل — الحد مكتوب على كل بطاقة.
- في المعركة: تقدر تضغط البطاقة ثم تضغط العدو، أو تسحب البطاقة بالماوس وترميها على العدو.
- العب بدون ما أحد يشرح لك. الهدف نعرف هل اللعبة واضحة لحالها.


3) وش نبيك تسوي
---------------
- العب 3 جولات على الأقل، أو ساعة لعب تقريبًا.
- لا تشيل هم الخسارة، الخسارة جزء من هالنوع من الألعاب.


4) بعد ما تخلص: أرسل لنا شيئين
------------------------------
أ) ملف سجل التجربة:
   من القائمة الرئيسية اضغط زر «سجل التجربة» (Playtest log)،
   بيفتح لك مجلد، أرسل الملف اللي اسمه playtest-log.txt
   (فيه أرقام بس: فزت أو خسرت، وين وصلت، وكم أخذت الجولة. ما فيه أي معلومات شخصية.)

ب) جاوب على هالأسئلة (ولو باختصار):
   1. كم جولة لعبت؟ وهل فزت بوحدة؟
   2. هل فهمت كيف تلعب بدون شرح؟ وش الشي اللي ما فهمته؟
   3. هل اللعبة ممتعة؟ متى حسيت بالملل أو الإحباط؟
   4. هل اللعبة سهلة، مناسبة، أو صعبة؟
   5. وش أكثر شي عجبك؟ ووش أكثر شي ما عجبك؟
   6. هل كملت الجولة الثانية بعد ما خسرت؟ ليش؟
   7. لو كانت على Steam، هل بتشتريها؟ وبكم تتوقع سعرها؟
   8. إذا لعبت بالعربي: هل فيه كلمة أو جملة غريبة؟
   9. إذا لعبت بيد تحكم: وش نوعها؟ وهل كل الأزرار اشتغلت صح؟


5) إذا علّقت أو انقفلت اللعبة
-----------------------------
أرسل هذا الملف:
   في Finder اضغط Cmd+Shift+G واكتب:
     ~/Library/Logs/mahersaudi/EmberDeck/
   وأرسل الملف Player.log
وقل لنا وش كنت تسوي لحظة ما صارت المشكلة.


-------------------------------------------------------------------------------

EmberDeck: Playtest build 0.1.0 (macOS)

Thanks for trying the game! Honest feedback is what matters most.

1) Opening it
   - Unzip, then open EmberDeck.app. It runs on Intel and Apple Silicon Macs.
   - The build isn't signed by Apple, so macOS will block it the first time:
     right-click the app > Open > Open. On newer macOS, go to System Settings >
     Privacy & Security, scroll down, and click "Open Anyway" next to EmberDeck.
     Or in Terminal: xattr -dr com.apple.quarantine /Applications/EmberDeck.app

2) Before you start
   - Arabic or English: Settings > Language.
   - Mouse, keyboard (arrows, Enter, Esc) or a gamepad all work.
   - Please play without anyone explaining it; we want to know if it teaches itself.

3) What to do
   - Play at least 3 runs, or about an hour. Losing is part of the genre.

4) When you're done, send two things
   a) The playtest log: on the main menu press "Playtest log", then send
      playtest-log.txt from the folder that opens (numbers only, nothing personal).
   b) Short answers:
      1. How many runs did you play? Did you win one?
      2. Could you work out how to play without help? What was unclear?
      3. Was it fun? When did you feel bored or frustrated?
      4. Too easy, about right, or too hard?
      5. What did you like most, and least?
      6. Did you start another run after losing? Why?
      7. If it were on Steam, would you buy it? For how much?
      8. Arabic players: anything that reads oddly?
      9. Gamepad players: which controller, and did every button work?

5) If it freezes or crashes
   In Finder press Cmd+Shift+G, enter ~/Library/Logs/mahersaudi/EmberDeck/
   and send Player.log, with what you were doing when it happened.
