-- =====================================================================
-- FitZone Training (FZT)
-- data_analysis_queries.sql : business reports for FitZone management
-- Run after database_creation_queries.sql and data_loading_queries.sql
--
-- Each query answers one business question. Run them one at a time in
-- MySQL Workbench (put the cursor in a query, then Ctrl+Enter).
-- =====================================================================
USE fitzone;


-- ---------------------------------------------------------------------
-- Report 1: Class popularity and fill rate
-- Which classes attract the most customers, and how full do they get?
-- Fill rate = seats taken / seats offered across all non-cancelled sessions.
-- ---------------------------------------------------------------------
SELECT  c.title,
        c.class_type,
        cat.name                                       AS category,
        COUNT(DISTINCT s.session_id)                   AS sessions_offered,
        COUNT(b.booking_id)                            AS seats_taken,
        COUNT(DISTINCT s.session_id) * c.max_capacity  AS seats_offered,
        ROUND(100 * COUNT(b.booking_id)
              / (COUNT(DISTINCT s.session_id) * c.max_capacity), 1) AS fill_rate_pct
FROM    fitness_class c
JOIN    category cat     ON cat.category_id = c.category_id
JOIN    class_session s  ON s.class_id = c.class_id AND s.status <> 'cancelled'
LEFT JOIN booking b      ON b.session_id = s.session_id
                        AND b.status IN ('confirmed', 'attended')
GROUP BY c.class_id, c.title, c.class_type, cat.name, c.max_capacity
ORDER BY fill_rate_pct DESC;


-- ---------------------------------------------------------------------
-- Report 2: Trainer performance (completed sessions)
-- Sessions taught, customers served, attendance rate, and revenue per trainer.
-- ---------------------------------------------------------------------
SELECT  CONCAT(u.first_name, ' ', u.last_name)               AS trainer,
        t.specialty,
        COUNT(DISTINCT s.session_id)                          AS sessions_taught,
        SUM(b.status = 'attended')                            AS customers_attended,
        SUM(b.status = 'no_show')                             AS no_shows,
        ROUND(100 * SUM(b.status = 'attended')
              / NULLIF(SUM(b.status IN ('attended','no_show')), 0), 1) AS attendance_rate_pct,
        SUM(CASE WHEN b.status = 'attended' THEN c.price ELSE 0 END) AS revenue
FROM    trainer t
JOIN    app_user u        ON u.user_id = t.trainer_id
JOIN    fitness_class c   ON c.trainer_id = t.trainer_id
JOIN    class_session s   ON s.class_id = c.class_id AND s.start_time < NOW()
LEFT JOIN booking b       ON b.session_id = s.session_id
GROUP BY t.trainer_id, u.first_name, u.last_name, t.specialty
ORDER BY revenue DESC;


-- ---------------------------------------------------------------------
-- Report 3: Revenue by category
-- Which workout categories earn the most (attended bookings only)?
-- ---------------------------------------------------------------------
SELECT  cat.name                     AS category,
        COUNT(b.booking_id)          AS paid_visits,
        SUM(c.price)                 AS total_revenue,
        ROUND(AVG(c.price), 2)       AS avg_price_per_visit,
        ROUND(100 * SUM(c.price) / (SELECT SUM(c2.price)
                                    FROM booking b2
                                    JOIN class_session s2 ON s2.session_id = b2.session_id
                                    JOIN fitness_class c2 ON c2.class_id = s2.class_id
                                    WHERE b2.status = 'attended'), 1) AS pct_of_revenue
FROM    booking b
JOIN    class_session s  ON s.session_id = b.session_id
JOIN    fitness_class c  ON c.class_id = s.class_id
JOIN    category cat     ON cat.category_id = c.category_id
WHERE   b.status = 'attended'
GROUP BY cat.category_id, cat.name
ORDER BY total_revenue DESC;


-- ---------------------------------------------------------------------
-- Report 4: Peak times
-- Which days and times are busiest? Helps plan staffing and new sessions.
-- ---------------------------------------------------------------------
SELECT  DAYNAME(s.start_time)               AS day_of_week,
        TIME_FORMAT(s.start_time, '%h:%i %p') AS start_time,
        COUNT(DISTINCT s.session_id)        AS sessions,
        COUNT(b.booking_id)                 AS bookings,
        ROUND(COUNT(b.booking_id) / COUNT(DISTINCT s.session_id), 1) AS avg_bookings_per_session
FROM    class_session s
LEFT JOIN booking b ON b.session_id = s.session_id
                   AND b.status IN ('confirmed', 'attended')
WHERE   s.status <> 'cancelled'
GROUP BY DAYNAME(s.start_time), WEEKDAY(s.start_time), TIME_FORMAT(s.start_time, '%h:%i %p'), TIME(s.start_time)
ORDER BY bookings DESC
LIMIT 10;


-- ---------------------------------------------------------------------
-- Report 5: Top customers
-- Most active customers by classes attended and total spend.
-- ---------------------------------------------------------------------
SELECT  CONCAT(u.first_name, ' ', u.last_name)  AS customer,
        u.email,
        cu.fitness_goal,
        SUM(b.status = 'attended')               AS classes_attended,
        SUM(b.status = 'confirmed')              AS upcoming_bookings,
        SUM(CASE WHEN b.status = 'attended' THEN c.price ELSE 0 END) AS total_spent,
        MAX(CASE WHEN b.status = 'attended' THEN s.start_time END)   AS last_visit
FROM    customer cu
JOIN    app_user u       ON u.user_id = cu.customer_id
LEFT JOIN booking b      ON b.customer_id = cu.customer_id
LEFT JOIN class_session s ON s.session_id = b.session_id
LEFT JOIN fitness_class c ON c.class_id = s.class_id
GROUP BY cu.customer_id, u.first_name, u.last_name, u.email, cu.fitness_goal
ORDER BY total_spent DESC
LIMIT 10;


-- ---------------------------------------------------------------------
-- Report 6: Cancellation and no-show rates by class
-- Which classes lose the most customers before or at the session?
-- ---------------------------------------------------------------------
SELECT  c.title,
        COUNT(b.booking_id)                                   AS total_bookings,
        SUM(b.status = 'cancelled')                           AS cancellations,
        SUM(b.status = 'no_show')                             AS no_shows,
        ROUND(100 * SUM(b.status = 'cancelled') / COUNT(b.booking_id), 1) AS cancel_rate_pct,
        ROUND(100 * SUM(b.status = 'no_show')
              / NULLIF(SUM(b.status IN ('attended','no_show')), 0), 1)    AS no_show_rate_pct
FROM    fitness_class c
JOIN    class_session s ON s.class_id = c.class_id
JOIN    booking b       ON b.session_id = s.session_id
GROUP BY c.class_id, c.title
HAVING  total_bookings > 0
ORDER BY cancel_rate_pct + no_show_rate_pct DESC;


-- ---------------------------------------------------------------------
-- Report 7: Upcoming sessions at risk (under 40% full)
-- Low-enrollment sessions to promote or consider cancelling.
-- Uses the v_session_availability view.
-- ---------------------------------------------------------------------
SELECT  v.session_id,
        c.title,
        CONCAT(u.first_name, ' ', u.last_name)  AS trainer,
        v.start_time,
        v.room_name,
        v.booked_count,
        v.max_capacity,
        ROUND(100 * v.booked_count / v.max_capacity, 1) AS pct_full
FROM    v_session_availability v
JOIN    fitness_class c ON c.class_id = v.class_id
JOIN    app_user u      ON u.user_id = c.trainer_id
WHERE   v.status = 'scheduled'
  AND   v.start_time > NOW()
  AND   c.class_type = 'group'
  AND   v.booked_count / v.max_capacity < 0.40
ORDER BY v.start_time;


-- ---------------------------------------------------------------------
-- Report 8: Group vs. private training comparison
-- How do the two business lines compare in volume and revenue?
-- ---------------------------------------------------------------------
SELECT  c.class_type,
        COUNT(DISTINCT c.class_id)              AS classes,
        COUNT(DISTINCT s.session_id)            AS sessions,
        SUM(b.status = 'attended')              AS visits,
        SUM(CASE WHEN b.status = 'attended' THEN c.price ELSE 0 END) AS revenue,
        ROUND(SUM(CASE WHEN b.status = 'attended' THEN c.price ELSE 0 END)
              / NULLIF(COUNT(DISTINCT CASE WHEN s.start_time < NOW() THEN s.session_id END), 0), 2)
                                                AS revenue_per_past_session
FROM    fitness_class c
JOIN    class_session s ON s.class_id = c.class_id AND s.status <> 'cancelled'
LEFT JOIN booking b     ON b.session_id = s.session_id
GROUP BY c.class_type;


-- ---------------------------------------------------------------------
-- Report 9: Room utilization
-- How much of each room's capacity is used by booked customers?
-- ---------------------------------------------------------------------
SELECT  r.name                                  AS room,
        r.capacity,
        COUNT(DISTINCT s.session_id)            AS sessions_hosted,
        ROUND(SUM(TIMESTAMPDIFF(MINUTE, s.start_time, s.end_time)) / 60, 1) AS hours_scheduled,
        ROUND(AVG(v.booked_count), 1)           AS avg_people_per_session
FROM    room r
LEFT JOIN class_session s          ON s.room_id = r.room_id AND s.status <> 'cancelled'
LEFT JOIN v_session_availability v ON v.session_id = s.session_id
GROUP BY r.room_id, r.name, r.capacity
ORDER BY hours_scheduled DESC;


-- ---------------------------------------------------------------------
-- Report 10: Demand by difficulty level
-- Are customers looking for beginner, intermediate, or advanced classes?
-- ---------------------------------------------------------------------
SELECT  c.difficulty,
        COUNT(DISTINCT c.class_id)        AS classes,
        COUNT(b.booking_id)               AS bookings,
        COUNT(DISTINCT b.customer_id)     AS unique_customers
FROM    fitness_class c
JOIN    class_session s ON s.class_id = c.class_id
LEFT JOIN booking b     ON b.session_id = s.session_id
                       AND b.status IN ('confirmed', 'attended')
GROUP BY c.difficulty
ORDER BY FIELD(c.difficulty, 'Beginner', 'Intermediate', 'Advanced');


-- ---------------------------------------------------------------------
-- Report 11: Category preference by age group
-- Which workouts appeal to which age groups? (uses customer date_of_birth)
-- ---------------------------------------------------------------------
SELECT  CASE
            WHEN TIMESTAMPDIFF(YEAR, cu.date_of_birth, CURDATE()) < 25 THEN 'Under 25'
            WHEN TIMESTAMPDIFF(YEAR, cu.date_of_birth, CURDATE()) < 30 THEN '25-29'
            WHEN TIMESTAMPDIFF(YEAR, cu.date_of_birth, CURDATE()) < 35 THEN '30-34'
            ELSE '35+'
        END                         AS age_group,
        cat.name                    AS category,
        COUNT(b.booking_id)         AS bookings
FROM    booking b
JOIN    customer cu      ON cu.customer_id = b.customer_id
JOIN    class_session s  ON s.session_id = b.session_id
JOIN    fitness_class c  ON c.class_id = s.class_id
JOIN    category cat     ON cat.category_id = c.category_id
WHERE   b.status IN ('confirmed', 'attended')
  AND   cu.date_of_birth IS NOT NULL
GROUP BY age_group, cat.name
ORDER BY age_group, bookings DESC;


-- ---------------------------------------------------------------------
-- Report 12: Customers at risk of leaving
-- Customers with no attended class in the last 7 days and nothing booked.
-- Good list for a "we miss you" email campaign.
-- ---------------------------------------------------------------------
SELECT  CONCAT(u.first_name, ' ', u.last_name)  AS customer,
        u.email,
        u.phone,
        MAX(CASE WHEN b.status = 'attended' THEN s.start_time END) AS last_attended
FROM    customer cu
JOIN    app_user u        ON u.user_id = cu.customer_id
LEFT JOIN booking b       ON b.customer_id = cu.customer_id
LEFT JOIN class_session s ON s.session_id = b.session_id
GROUP BY cu.customer_id, u.first_name, u.last_name, u.email, u.phone
HAVING  (last_attended IS NULL OR last_attended < NOW() - INTERVAL 7 DAY)
   AND  COALESCE(SUM(b.status = 'confirmed' AND s.start_time > NOW()), 0) = 0
ORDER BY last_attended;
