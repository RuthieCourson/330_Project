-- =====================================================================
-- FitZone Training (FZT)
-- data_loading_queries.sql : loads sample data. Run after database_creation_queries.sql
--
-- Every seeded account uses the password:  Password123!
-- Sessions are generated relative to CURDATE() so the site always has
-- upcoming classes (next 3 weeks) and booking history (past 2 weeks).
-- =====================================================================
USE fitzone;

SET @pw = '100000.Rml0Wm9uZVNlZWRTYWx0MQ==.w25pUmWr68/1eBAq5BKgyYaXfftNlSjJproLG1wH16s=';

-- ---------------------------------------------------------------------
-- Users: 1-5 trainers, 6-22 customers (21-22 are lapsed members)
-- ---------------------------------------------------------------------
INSERT INTO app_user (user_id, first_name, last_name, email, phone, password_hash, role) VALUES
(1,  'Maya',    'Thompson', 'maya.thompson@fitzone.com',  '205-555-0101', @pw, 'trainer'),
(2,  'Jordan',  'Reed',     'jordan.reed@fitzone.com',    '205-555-0102', @pw, 'trainer'),
(3,  'Carlos',  'Mendez',   'carlos.mendez@fitzone.com',  '205-555-0103', @pw, 'trainer'),
(4,  'Aisha',   'Patel',    'aisha.patel@fitzone.com',    '205-555-0104', @pw, 'trainer'),
(5,  'Ben',     'Walker',   'ben.walker@fitzone.com',     '205-555-0105', @pw, 'trainer'),
(6,  'Emily',   'Carter',   'emily.carter@email.com',     '205-555-0201', @pw, 'customer'),
(7,  'Marcus',  'Johnson',  'marcus.johnson@email.com',   '205-555-0202', @pw, 'customer'),
(8,  'Sofia',   'Ramirez',  'sofia.ramirez@email.com',    '205-555-0203', @pw, 'customer'),
(9,  'Tyler',   'Brooks',   'tyler.brooks@email.com',     '205-555-0204', @pw, 'customer'),
(10, 'Hannah',  'Kim',      'hannah.kim@email.com',       '205-555-0205', @pw, 'customer'),
(11, 'Andre',   'Williams', 'andre.williams@email.com',   '205-555-0206', @pw, 'customer'),
(12, 'Grace',   'Nguyen',   'grace.nguyen@email.com',     '205-555-0207', @pw, 'customer'),
(13, 'Liam',    'OConnor',  'liam.oconnor@email.com',     '205-555-0208', @pw, 'customer'),
(14, 'Priya',   'Shah',     'priya.shah@email.com',       '205-555-0209', @pw, 'customer'),
(15, 'Noah',    'Davis',    'noah.davis@email.com',       '205-555-0210', @pw, 'customer'),
(16, 'Olivia',  'Martinez', 'olivia.martinez@email.com',  '205-555-0211', @pw, 'customer'),
(17, 'Ethan',   'Lee',      'ethan.lee@email.com',        '205-555-0212', @pw, 'customer'),
(18, 'Zoe',     'Robinson', 'zoe.robinson@email.com',     '205-555-0213', @pw, 'customer'),
(19, 'Caleb',   'Turner',   'caleb.turner@email.com',     '205-555-0214', @pw, 'customer'),
(20, 'Ava',     'Mitchell', 'ava.mitchell@email.com',     '205-555-0215', @pw, 'customer'),
(21, 'Jason',   'Price',    'jason.price@email.com',      '205-555-0216', @pw, 'customer'),
(22, 'Mia',     'Foster',   'mia.foster@email.com',       '205-555-0217', @pw, 'customer');

INSERT INTO trainer (trainer_id, bio, specialty, years_experience, certifications) VALUES
(1, 'Maya blends breath work and mobility to help members move better and stress less.', 'Yoga & Pilates', 9, 'RYT-500, PMA-CPT'),
(2, 'Former amateur boxer who builds conditioning through high-energy intervals.', 'HIIT & Boxing', 7, 'NASM-CPT, USA Boxing Coach'),
(3, 'Strength coach focused on safe technique and progressive overload.', 'Strength & Olympic Lifting', 12, 'NSCA-CSCS, USAW-L1'),
(4, 'Indoor cycling lead who programs every ride to the beat.', 'Indoor Cycling', 5, 'Schwinn Certified, ACE-GFI'),
(5, 'Personal trainer specializing in body recomposition and athletic conditioning.', 'Personal Training & Conditioning', 10, 'ACSM-CPT, Precision Nutrition L1');

INSERT INTO customer (customer_id, date_of_birth, fitness_goal, emergency_contact) VALUES
(6,  '1998-04-12', 'Run a half marathon',          'Paul Carter 205-555-0301'),
(7,  '1990-09-30', 'Build muscle',                 'Dana Johnson 205-555-0302'),
(8,  '2001-01-22', 'Improve flexibility',          'Luis Ramirez 205-555-0303'),
(9,  '1995-07-08', 'Lose 15 pounds',               'Kara Brooks 205-555-0304'),
(10, '1999-11-17', 'Reduce stress',                'Joon Kim 205-555-0305'),
(11, '1988-02-03', 'Stay active',                  'Tasha Williams 205-555-0306'),
(12, '2002-06-25', 'Get stronger for volleyball',  'Minh Nguyen 205-555-0307'),
(13, '1993-12-01', 'Learn to box',                 'Sean OConnor 205-555-0308'),
(14, '1997-03-14', 'Improve posture',              'Ravi Shah 205-555-0309'),
(15, '1985-08-19', 'Lower blood pressure',         'Mia Davis 205-555-0310'),
(16, '2000-10-05', 'Tone up',                      'Rosa Martinez 205-555-0311'),
(17, '1996-05-27', 'Increase endurance',           'Grace Lee 205-555-0312'),
(18, '2003-02-11', 'Try new workouts',             'Ian Robinson 205-555-0313'),
(19, '1991-07-29', 'Deadlift 400 lb',              'Amy Turner 205-555-0314'),
(20, '1994-09-09', 'Recover from knee injury',     'Jack Mitchell 205-555-0315'),
(21, '1989-04-30', 'Get back in shape',            'Laura Price 205-555-0316'),
(22, '2000-12-15', 'Train for a 5K',               'Dan Foster 205-555-0317');

-- ---------------------------------------------------------------------
-- Login sessions (sample history). Tokens are random, so they can't be
-- reused to log in; the website creates real ones on each login.
-- ---------------------------------------------------------------------
INSERT INTO auth_session (token, user_id, created_at, expires_at) VALUES
(SHA2(CONCAT('seed-1-',  RAND()), 256), 1,  NOW() - INTERVAL 10 DAY, NOW() - INTERVAL 3 DAY),
(SHA2(CONCAT('seed-6-',  RAND()), 256), 6,  NOW() - INTERVAL 8 DAY,  NOW() - INTERVAL 1 DAY),
(SHA2(CONCAT('seed-7-',  RAND()), 256), 7,  NOW() - INTERVAL 5 DAY,  NOW() + INTERVAL 2 DAY),
(SHA2(CONCAT('seed-2-',  RAND()), 256), 2,  NOW() - INTERVAL 2 DAY,  NOW() + INTERVAL 5 DAY),
(SHA2(CONCAT('seed-10-', RAND()), 256), 10, NOW() - INTERVAL 1 DAY,  NOW() + INTERVAL 6 DAY);

-- ---------------------------------------------------------------------
-- Lookup data
-- ---------------------------------------------------------------------
INSERT INTO category (category_id, name, description) VALUES
(1, 'Yoga',              'Flexibility, balance and mindfulness'),
(2, 'HIIT',              'High-intensity interval training'),
(3, 'Strength',          'Resistance and weight training'),
(4, 'Cycling',           'Indoor cycling and spin'),
(5, 'Pilates',           'Core strength and control'),
(6, 'Boxing',            'Boxing technique and conditioning'),
(7, 'Personal Training', 'One-on-one sessions with a trainer');

INSERT INTO room (room_id, name, capacity) VALUES
(1, 'Studio A',     25),
(2, 'Studio B',     20),
(3, 'Cycle Room',   18),
(4, 'Weight Floor', 15),
(5, 'PT Suite',      2);

-- ---------------------------------------------------------------------
-- Classes (10 group, 4 private)
-- ---------------------------------------------------------------------
INSERT INTO fitness_class (class_id, trainer_id, category_id, title, description, class_type, difficulty, duration_min, price, max_capacity) VALUES
(1,  1, 1, 'Sunrise Vinyasa Yoga',        'Start the day with a flowing sequence that links breath and movement.',            'group',   'Beginner',     60, 15.00, 20),
(2,  1, 1, 'Power Yoga Flow',             'A faster, strength-focused yoga practice for those comfortable with the basics.',  'group',   'Intermediate', 60, 18.00, 20),
(3,  1, 5, 'Core Pilates',                'Mat Pilates targeting deep core muscles, posture and control.',                    'group',   'Beginner',     45, 15.00, 15),
(4,  2, 2, 'HIIT Blast',                  'Short bursts of all-out effort with brief recovery. Expect to sweat.',             'group',   'Advanced',     45, 20.00, 20),
(5,  2, 6, 'Boxing Fundamentals',         'Learn stance, footwork, and punch combinations on the heavy bag.',                 'group',   'Beginner',     60, 22.00, 16),
(6,  3, 3, 'Strength 101',                'Master the squat, hinge, push and pull with coached technique.',                   'group',   'Beginner',     50, 18.00, 12),
(7,  3, 3, 'Olympic Lifting Lab',         'Snatch and clean & jerk drills for experienced lifters.',                          'group',   'Advanced',     75, 25.00, 10),
(8,  4, 4, 'Rhythm Ride',                 'A music-driven spin class with climbs, sprints and choreography.',                 'group',   'Intermediate', 45, 16.00, 18),
(9,  4, 4, 'Endurance Ride',              'Longer intervals that build aerobic base and mental toughness.',                   'group',   'Advanced',     60, 18.00, 18),
(10, 5, 2, 'Total Body Conditioning',     'Circuit-style workout mixing cardio, kettlebells and bodyweight moves.',           'group',   'Intermediate', 50, 18.00, 20),
(11, 5, 7, '1-on-1 Personal Training',    'A personalized session with Ben built around your goals.',                         'private', 'Beginner',     60, 65.00,  1),
(12, 1, 7, 'Private Yoga Therapy',        'Individual yoga session focused on mobility, recovery and stress relief.',         'private', 'Beginner',     60, 60.00,  1),
(13, 3, 7, 'Private Strength Coaching',   'One-on-one lifting technique and program design with Carlos.',                     'private', 'Intermediate', 60, 70.00,  1),
(14, 2, 6, 'Private Boxing Mitt Work',    'Pad work and personalized boxing coaching.',                                       'private', 'Intermediate', 45, 55.00,  1);

-- ---------------------------------------------------------------------
-- Sessions: weekly schedule expanded across 14 days ago -> 21 days ahead
-- weekday: 0 = Monday ... 6 = Sunday (MySQL WEEKDAY())
-- ---------------------------------------------------------------------
CREATE TEMPORARY TABLE tmp_schedule (class_id INT, weekday TINYINT, start_at TIME, room_id INT);
INSERT INTO tmp_schedule VALUES
(1,0,'06:30',1),(1,2,'06:30',1),(1,4,'06:30',1),
(2,1,'17:30',1),(2,3,'17:30',1),
(3,2,'12:00',2),(3,5,'09:00',2),
(4,0,'18:00',2),(4,2,'18:00',2),
(5,1,'19:00',2),(5,3,'19:00',2),
(6,0,'12:00',4),(6,3,'12:00',4),
(7,5,'10:30',4),
(8,1,'06:30',3),(8,3,'06:30',3),(8,5,'08:00',3),
(9,6,'09:00',3),
(10,4,'17:30',1),
(11,0,'08:00',5),(11,2,'08:00',5),(11,4,'08:00',5),(11,0,'14:00',5),(11,2,'14:00',5),
(12,1,'10:00',5),(12,5,'13:00',5),
(13,1,'15:00',5),(13,3,'15:00',5),
(14,2,'16:00',5),(14,4,'16:00',5);

INSERT INTO class_session (class_id, room_id, start_time, end_time, status)
WITH RECURSIVE days AS (
    SELECT DATE_SUB(CURDATE(), INTERVAL 14 DAY) AS d
    UNION ALL
    SELECT DATE_ADD(d, INTERVAL 1 DAY) FROM days WHERE d < DATE_ADD(CURDATE(), INTERVAL 21 DAY)
)
SELECT  t.class_id,
        t.room_id,
        TIMESTAMP(d.d, t.start_at),
        TIMESTAMP(d.d, t.start_at) + INTERVAL c.duration_min MINUTE,
        IF(TIMESTAMP(d.d, t.start_at) < NOW(), 'completed', 'scheduled')
FROM    days d
JOIN    tmp_schedule t  ON t.weekday = WEEKDAY(d.d)
JOIN    fitness_class c ON c.class_id = t.class_id
ORDER BY TIMESTAMP(d.d, t.start_at);

DROP TEMPORARY TABLE tmp_schedule;

-- One upcoming session cancelled by the trainer (demo data)
UPDATE class_session
SET    status = 'cancelled'
WHERE  class_id = 7 AND start_time > NOW()
ORDER BY start_time
LIMIT 1;

-- ---------------------------------------------------------------------
-- Bookings: deterministic "random" mix of customers for each session,
-- filling group classes to ~35-70% and about half of private slots.
-- ---------------------------------------------------------------------
INSERT INTO booking (customer_id, session_id, confirmation_code, status, booked_at, cancelled_at)
SELECT  customer_id,
        session_id,
        UPPER(LEFT(MD5(CONCAT('FZT', customer_id, '-', session_id)), 8)),
        CASE
            WHEN start_time < NOW() AND (customer_id + session_id) % 9 = 0 THEN 'no_show'
            WHEN start_time < NOW()                                         THEN 'attended'
            WHEN (customer_id * session_id) % 11 = 0                        THEN 'cancelled'
            ELSE 'confirmed'
        END,
        start_time - INTERVAL (2 + (customer_id + session_id) % 6) DAY,
        CASE WHEN start_time >= NOW() AND (customer_id * session_id) % 11 = 0
             THEN start_time - INTERVAL 1 DAY END
FROM (
    SELECT  cu.customer_id,
            s.session_id,
            s.start_time,
            fc.max_capacity,
            fc.class_type,
            ROW_NUMBER() OVER (PARTITION BY s.session_id
                               ORDER BY (cu.customer_id * 31 + s.session_id * 17) % 97) AS rn
    FROM    class_session s
    JOIN    fitness_class fc ON fc.class_id = s.class_id
    CROSS JOIN customer cu
    WHERE   s.status <> 'cancelled'
      AND   cu.customer_id <= 20
) x
WHERE   (class_type = 'group'   AND rn <= LEAST(15, FLOOR(max_capacity * (0.35 + (session_id % 4) * 0.12))))
   OR   (class_type = 'private' AND rn = 1 AND session_id % 2 = 0);

-- Lapsed members (21, 22): only an early visit and a cancellation, nothing
-- upcoming. They show up in the "customers at risk" analysis report.
INSERT INTO booking (customer_id, session_id, confirmation_code, status, booked_at, cancelled_at)
SELECT 21, MIN(session_id), 'LAPSE021', 'attended', MIN(start_time) - INTERVAL 3 DAY, NULL
FROM   class_session WHERE class_id = 1 AND status = 'completed'
UNION ALL
SELECT 22, MIN(session_id), 'LAPSE022', 'cancelled', MIN(start_time) - INTERVAL 4 DAY, MIN(start_time) - INTERVAL 1 DAY
FROM   class_session WHERE class_id = 8 AND status = 'completed';
