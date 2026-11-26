# Masterclass Quiz

A live quiz app for teaching a group: in a Masterclass, a Host presents each Workshop of a Course, then the group answers a quiz on it and each person learns which Lessons to revisit.

## Course content

**Course**:
A body of teaching material made of ordered Workshops (e.g. "Testing Fundamentals").
_Avoid_: Training, Masterclass (a Masterclass is one group being taught a Course)

**Workshop**:
One part of a Course, taught in a single talk and followed by a Session (e.g. "Unit tests", "Test doubles").
_Avoid_: Section, Chapter, Module, Lesson (a Lesson is one piece inside a Workshop)

**Lesson**:
A single piece of material inside a Workshop, the smallest thing a Participant can be pointed back to (e.g. "Writing your first test"). An exercise and its solution together are one Lesson.
_Avoid_: Page, Article, Topic

**Question**:
A prompt with answer options, exactly one of them correct, that tests one Lesson. A Workshop's Questions are all the Questions of its Lessons.
_Avoid_: Item, Task

**Difficulty**:
How hard a Question is: Easy, Medium or Hard. Harder Questions give more time to answer and are worth more points.
_Avoid_: Level, Weight

**Import file**:
One file describing a whole Course (its Workshops, Lessons and Questions), which the Host uploads to bring the Course into the app.
_Avoid_: Course file, Upload, Export

## Live quiz

**Masterclass**:
One group being taught a Course in a single meeting, ending when its last Workshop's Session ends or when the Host closes it earlier; it holds that group's Sessions and Results (e.g. "Dev team 2026"). The same Course can be taught in many Masterclasses. It is always Masterclass Draft, Masterclass Open or Masterclass Closed.
_Avoid_: Course, Cohort, Class, Group

**Masterclass Draft**:
A Masterclass the Host has created but not yet opened; it has no Join code, so nobody can join.
_Avoid_: Draft (alone), New, Pending

**Masterclass Open**:
A Masterclass that has a Join code, so Participants can join and its Sessions can run.
_Avoid_: Open (alone), Active, Live, Started

**Masterclass Closed**:
A Masterclass that has ended, either on its own when its last Workshop's Session ends or by the Host at the close of the meeting; nobody can join and no more Sessions run, but its Results and Recaps stay available.
_Avoid_: Closed (alone), Finished, Archived, Ended

**Next Workshop**:
The first Workshop of the Course whose Session has not run yet in a Masterclass: the one the Host presents next and the only one whose Session can start. Workshops are taught in Course order, none skipped.
_Avoid_: Current Workshop, Active Workshop

**Session**:
The one live quiz for a Workshop within a Masterclass, run by a Host after presenting it, using a random selection of that Workshop's Questions in the Difficulty mix the Host sets for it.
_Avoid_: Game, Round, Quiz

**Answer**:
A Participant's response to one Question in a Session; its outcome is Correct, Incorrect or Timed out (no answer before time ran out).
_Avoid_: Submission, Response, Not answered, Skipped

**Question Reveal**:
The moment a Question in a Session stops taking Answers and everyone sees the correct option; it comes when time runs out or once every Participant has answered. Answers only count towards Points, the Session leaderboard and the Masterclass leaderboard after it.
_Avoid_: Reveal (alone), Result, Show answer

**Points**:
What a Correct Answer earns: more for a harder Difficulty and for answering faster; Incorrect and Timed out earn none.
_Avoid_: Score, XP

**Session leaderboard**:
The Participants of one Session ranked by the Points they earned in that Session, highest first; equal Points share a rank.
_Avoid_: Round leaderboard, Scoreboard

**Masterclass leaderboard**:
The Participants of a Masterclass ranked by their Points summed over all its Sessions, highest first; equal Points share a rank.
_Avoid_: Overall leaderboard, Ranking, Scoreboard, Standings

**Results**:
The Host's view of a Masterclass: how each Session's Questions went and every Participant's Points, with a way into each Recap.
_Avoid_: Report, Statistics, Dashboard

**Recap**:
A Participant's personal summary of a Masterclass, by Workshop: the Questions they got Incorrect or Timed out, each pointing to the Lesson to revisit, or Not attended for Workshops whose Session they missed.
_Avoid_: Report (ambiguous with Results), Feedback, Summary

**Not attended**:
A Participant missing a Workshop's whole Session, either by joining the Masterclass later or because the Masterclass closed before that Workshop's Session ran; their Recap points them to the Workshop's Lessons instead.
_Avoid_: Absent, Skipped

**Perfect score**:
A Participant answering every Question in a Session Correct; shown as a badge in their Recap.
_Avoid_: Flawless, Clean sheet

## People

**Host**:
The person who presents a Workshop and runs its Session.
_Avoid_: Admin, Presenter, Teacher

**Participant**:
A person in a Masterclass answering Questions in its Sessions, known only by an assigned Nickname that stays the same across the whole Masterclass.
_Avoid_: Player, User, Student, Attendee

**Join code**:
The short code Participants enter to join a Masterclass.
_Avoid_: PIN, Room code, Session code

**Nickname**:
The name the app assigns to a Participant on joining; Participants do not choose it.
_Avoid_: Username, Display name
